# Shared Patterns — AIMO Park WSO2 → Frends PoC

Cross-pattern architectural notes distilled from the 6 Synapse sample files. Review this before
designing a new integration's Frends equivalent; reuse a pattern below rather than inventing a new
one unless there's a documented reason.

## Pattern: HTTP/SOAP backend call → Frends HTTP Task

All 6 samples ultimately call the same fictional backend, `SimpleStockQuoteService`
(`http://localhost:9000/services/SimpleStockQuoteService`), a SOAP 1.1 service. In every sample the
Synapse `<send><endpoint><address uri="..."/></endpoint></send>` (or `<endpoint key="...">`
indirection, or `<wsdl>` resolution) collapses to the same underlying Frends primitive:

- **Frends equivalent:** `HTTP.Request` Task configured for the SOAP endpoint (POST, SOAPAction
  header, XML body), or a WSDL-based SOAP task if Frends' SOAP tooling is preferred over raw HTTP.
- Applies to: SYN-150, SYN-001, SYN-003, SYN-004, SYN-056, SYN-300.

## Pattern: Content-based routing → Decision (ExclusiveGateway) or Choice/Switch

Synapse `<filter>` (XPath/regex on a message property) and `<switch>` (multi-case regex match) both
express the same intent: branch the flow based on inspecting the inbound message.

- **Frends equivalent:** `ExclusiveGateway` with an expression evaluated on the routed field (per
  [[feedback_frends_gateways]] — expression goes on the gateway itself, not the outgoing flows, for
  Exclusive). A `<switch>` with N cases maps to N outgoing branches off one gateway rather than
  nested filters.
- Applies to: SYN-001 (`<filter>`), SYN-004 (`<switch>` with 3 cases).

## Pattern: Registry-style reuse (named endpoint/sequence, referenced by key) → Environment Variables + Subprocess

Synapse's `<localEntry>`, `<endpoint name="...">`, and `<sequence name="...">` defined once and
referenced elsewhere by `key="..."` is a **governance/reuse pattern**, not just plumbing — and it's
the pattern AIMO explicitly says it lacks today (RFI §11, "no structures for re-use").

- **Frends equivalent:** the target URL/config becomes a Frends **Environment Variable** (not
  hardcoded — see Development Principles); the reusable Synapse `<sequence>` becomes a Frends
  **subprocess** invoked via Call Activity, catalogued in `reusable-components.md`.
- Applies to: SYN-003 directly; this is also the target pattern to retrofit into every other sample
  once a shared subprocess exists (see `reusable-components.md`).

## Pattern: Fault/error sequence → Process-level error handler + Catch

Synapse's implicit default `fault` sequence plus a named `onError` override on a specific inner
`<sequence>` is a two-tier error model: a landscape-wide default, overridable per-branch.

- **Frends equivalent:** a process-level error handler (`process_set_error_handler`) for the
  default case, plus a scoped `Catch` shape around the specific branch that needs custom handling —
  mirroring Synapse's override semantics without needing try/catch-style Code elements
  (see [[feedback_frends_no_try_catch]]).
- Applies to: SYN-004.

## Pattern: WSDL-resolved endpoint vs. hardcoded URL → Environment Variable, resolved at deploy time

Synapse's `<wsdl uri="..." service="..." port="..."/>` endpoint resolves the actual call target from
a WSDL at runtime instead of a literal URL. Frends doesn't have a direct WSDL-resolution primitive
in the same sense; the practical equivalent is to resolve the concrete endpoint URL once (at
integration-build time, from the WSDL) and externalize it as an Environment Variable, same as any
other endpoint.

- **Open question:** if AIMO's real integrations rely on *dynamic* WSDL resolution (e.g. UDDI-style
  service discovery, not just "read this one static WSDL file"), that would need a different design
  — flag as a question for AIMO once real assets are available.
- Applies to: SYN-056.

## Pattern: Timer-triggered message injection → Schedule Trigger

Synapse's `<task class="...MessageInjector"><trigger interval="N"/></task>` fires a synthetic
request on a timer rather than responding to an inbound call.

- **Frends equivalent:** native Frends `Schedule Trigger` on the process, with the injected SOAP
  request body as a static payload (or built via Code/Template element if AIMO's real equivalents
  vary the payload).
- Applies to: SYN-300.

## Complex-tier patterns (from the 9-sample "complex" corpus, part 2)

These 9 samples cover the mediators the original 6 didn't: caching, external-policy security,
embedded scripting, REST API shape modeling, DB-enriched routing, scatter-gather, store-and-forward,
load-balance/failover, and cross-sequence continuation. Several surfaced genuine Frends capability
questions rather than pure translation exercises — flagged explicitly below rather than assumed.

### Pattern: Request/response caching → explicit cache-check gateway + Shared State Task

Synapse's `<cache>` mediator pair (check on the way in, collector on the way out) is implicit —
nothing in the flow visibly shows caching is happening. Frends equivalent: an explicit
`ExclusiveGateway` on cache-hit/miss, which is more maintainable (visible in the diagram) at the
cost of one extra shape. **Resolved (2026-09-16, via docs.frends.com):** the cache store itself is
a native **Shared State Task** (`process_add_shared_state`) — TTL 1 minute to 30 days,
process-specific or Agent-Group-wide scope, `AddOrUpdate`/`GetOrAdd`/`TryGetValue`/`UpdateTTL`
operations. Note: sub-minute TTLs (like SYN-420's source 20s) fall below Shared State's minimum
granularity — round up or flag as a real requirement gap if sub-minute caching genuinely matters.
`HTTP.Request`'s `CacheHttpClients` option is unrelated (HttpClient connection reuse, not response
caching) — don't conflate the two. Applies to: SYN-420.

### Pattern: WS-Security → `Frends.Soap.Request` (conditional on which profile)

**Verified via docs.frends.com, not assumed:** Frends has a native `Frends.Soap.Request` Task
(v1.1.0, Frends 5.7+) with `IncludeWsSecurity` support for the **UsernameToken + Timestamp**
profile — a real native feature. It does **not** natively enforce arbitrary WS-SecurityPolicy
assertions (X.509 signing/encryption of specific SOAP elements); that would need a custom build on
top of the generic `SignXml` Task, with no guaranteed conformance. Applies to: SYN-100, SYN-156
(the secure backend leg). **Which branch applies depends entirely on the real policy content**,
unknown for both samples in this corpus.

### Pattern: Embedded custom script (JS) → Code element (structure-only until content is known)

Synapse's `<script>` mediator invokes named JS functions loaded from the registry. Frends
equivalent: a Code element (`UseStatementMode = true`) at the same flow position. **This pattern
cannot be migrated in substance without the actual script source** — only the wiring/position is
portable without it. Applies to: SYN-350.

### Pattern: REST API with multiple resources → one Frends HTTP-triggered process per resource

WSO2's `<api>` construct with multiple `<resource>`s (different paths/methods/semantics) has no
single-process Frends equivalent, since `process_add_http_trigger` binds one route+method per
trigger. Design decision: **one Frends process per resource**, sharing a route-prefix convention
and reusable backend-call components, rather than one process with internal path/method dispatch.
Applies to: SYN-800.

### Pattern: Fire-and-forget response (`FORCE_SC_ACCEPTED`/`OUT_ONLY`) → Intermediate Return

Synapse signals "ack the caller now, keep processing independently" via Axis2-scope properties.
**Resolved (2026-09-16, via docs.frends.com):** Frends has a native **Intermediate Return** shape
(`process_add_intermediate_return`) that sends the HTTP response and closes the connection
*without* ending the process — execution genuinely continues to the eventual final Return. This is
a real native feature, confirmed, not a workaround. Caveat: intended for use only once per process
instance (extras are disregarded); a plain `Return` (not Intermediate) still ends the process, so
don't confuse the two shapes. Applies to: SYN-800 (Resource 2), SYN-700 (Process 1).

### Pattern: DB-enriched routing → SQL Task + collapsed decision

Synapse's `<dblookup>` feeding a `<switch>` is a real, common pattern. Worth actively simplifying
near-duplicate switch cases (see SYN-360: 3 of 4 cases were functionally identical) rather than
porting them 1:1 — a documented, deliberate simplification, not an oversight. Always preserve
parameterized SQL (never string-concatenate a looked-up value into a query). Applies to: SYN-360.

### Pattern: Scatter-gather (iterate/aggregate) → `Foreach` + collected results (sequential only — confirmed limitation)

No flat/linear Frends equivalent — genuinely needs `Foreach`. **Resolved (2026-09-16, via
docs.frends.com): Frends' `Foreach` is sequential-only.** True parallelism exists only via
design-time-fixed parallel branches (multiple connections drawn from one shape), not a
dynamically-sized fan-out. This is a real, confirmed performance limitation (not a correctness
issue) for any real integration needing N-way concurrent dispatch where N varies at runtime and is
large/latency-sensitive — flag explicitly rather than silently accept the slower sequential
behaviour. Partial-failure semantics (fail the whole batch vs. partial success) must still be an
explicit design decision — the source sample leaves this completely unspecified. Applies to:
SYN-400.

### Pattern: Store-and-forward (message store + scheduled processor) → Queue Trigger + consumer process

Synapse's message store needs no external broker by default (durability depends on what backs it,
often unspecified in samples). The Frends-native equivalent likely needs a **real external
queue/broker as new infrastructure** — this is a genuine infrastructure delta to flag to AIMO, not
a like-for-like swap. Treat the "storing" process and the "forwarding" process as one logical unit
in documentation even though they're separate deployable Frends processes — do not let them drift
apart in future edits. Applies to: SYN-700.

### Pattern: Load-balance / failover endpoint trees → **confirmed genuine capability gap**

Synapse's endpoint composition (session affinity → load-balance → failover groups → addresses) has
**no native Frends equivalent — confirmed (2026-09-16, via docs.frends.com)**, not just suspected.
`HTTP.Request`'s only outbound resilience option is Task-level retry of the *same* call (up to 10x,
exponential backoff); Frends' "High Availability" feature load-balances *incoming triggers across
Agents*, a completely different axis, and must not be conflated with outbound endpoint
load-balancing. A faithful port is a genuinely custom build: Shared State Task for session-pinning
(now a confirmed native mechanism) + hand-rolled Decision/Catch logic for group selection and
failover — no task configuration shortcut exists. **This remains the single highest-effort pattern
across both PoC corpora**, and its business value is unconfirmed: check whether AIMO's real estate
(SaaS-heavy — Salesforce/NetSuite manage their own HA) even needs this pattern before investing
further design or build effort. Applies to: SYN-055.

### Pattern: Enrich-based state save/restore → plain process variables (deliberate simplification)

Synapse's `<enrich>` mediator exists to work around a real constraint: only one "current message"
is active at a time, so anything that must survive past the next call has to be saved to a
property first and restored later. **Frends has no such constraint** — process variables can hold
multiple named values simultaneously for a process's whole execution. Do not port `<enrich>`
choreography literally; replace it with ordinary named variables holding each value that needs to
survive. This is a deliberate design improvement, documented per Continuous Traceability, not a
missed nuance. Applies to: SYN-156 (5 enrich operations collapse to ~4 plain variables).

### Pattern: Cross-sequence continuation (`<send receive="OtherSequence">`) → one linear process

Synapse can split what is logically one flow across multiple named top-level sequences, connected
only by `receive="..."` attributes that aren't visually obvious from the XML structure alone. This
is a real governance/readability risk (a naive per-file AI conversion could easily treat the
continuation target as an unrelated, orphaned artifact) — and a red flag to watch for explicitly
when reviewing AIMO's real 126 WSO2 integrations, not just a corpus curiosity. The Frends
equivalent typically collapses cleanly into **one linear process**, since Frends has no reason to
split a single logical flow across separately-triggered artifacts. Applies to: SYN-156.

## Notes for the real AIMO estate (not yet validated)

These patterns are confirmed only against public Synapse samples (15 across both corpus tiers).
AIMO's real WSO2 estate (126 integrations, maintained by Vivicta) may use additional Synapse
mediators/patterns not represented here — e.g. class mediators (custom Java beyond simple
scripting), JMS-specific endpoints, XSLT/Smooks transformation mediators, or PayloadFactory at
scale. Do not assume the patterns above are exhaustive once real assets are reviewed.

**Capability-question resolution status (all checked against docs.frends.com on 2026-09-16, not
memory):**

| Question | Resolution |
|---|---|
| Cache store mechanism (SYN-420) | **Native** — Shared State Task, TTL 1min–30 days |
| Fire-and-forget / detached Return (SYN-800, SYN-700) | **Native** — Intermediate Return |
| Foreach concurrency (SYN-400) | **Confirmed sequential-only** — real, minor limitation |
| WS-Security (SYN-100, SYN-156) | **Native for UsernameToken+Timestamp; not for full policy-driven signing/encryption** — two-branch answer |
| Load-balance/failover endpoint composition (SYN-055) | **Confirmed gap — no native primitive** |

Of five open capability questions surfaced by the complex-tier PoC, four resolved in Frends' favor
(three fully native, one workable-with-a-known-limitation) and one (load-balance/failover) is a
confirmed real gap requiring a custom build. This ratio — mostly native, one real gap — is itself
useful evidence for the RFI.
