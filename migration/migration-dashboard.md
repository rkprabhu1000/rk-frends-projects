# Migration Dashboard — AIMO Park WSO2 → Frends (RFI PoC)

## ⚠️ Scope framing — read before using this dashboard

This is **not** a real customer migration programme yet. AIMO Park has not shared any of its actual
WSO2/Synapse configuration. The only source material available is a 6-file corpus of **public,
official Apache Synapse sample configurations** (`synapse.apache.org/userguide/samples`), placed in
`WSO2 sample configs/` specifically to prototype whether Frends' general-purpose LLM code-generation
can plausibly convert WSO2/Synapse XML into Frends processes — as a **PoC exhibit inside the AIMO
Park RFI response**, not a demonstration of a shipped "WSO2→Frends converter" feature (none exists
today).

Everything below tracks these **15 synthetic pattern tests** (6 in `WSO2 sample configs/`, 9 more
in `WSO2 sample configs/complex/`), not AIMO's actual 126 WSO2 + 228 P2P integration estate. Real
inventory/assessment of AIMO's estate cannot start until AIMO shares actual exports — see
**Open Questions / Blockers** below for what's still missing from the RFI Q&A.

## Real-world context (from AIMO Park RFI Q&A, `Questions & Answers to AIMO Park from RFI.docx`)

- **Estate size:** 126 WSO2 integrations + 228 point-to-point (P2P) integrations (scope not yet confirmed as final — may include to-be-retired integrations; AIMO wants help deciding).
- **Systems:** Salesforce (SaaS/vendor cloud), NetSuite (SaaS/vendor cloud) — WSO2 handles all integration to NetSuite; core parking platform is AIMO's own, on AWS, Kubernetes + MongoDB.
- **Vendor base:** WSO2 estate built/maintained by **Vivicta** (external vendor); all other (P2P) integrations built/maintained **in-house**.
- **Triggers:** "Many are scheduled, some are event-triggered, but currently there are no clear rules or principles for triggers" — i.e. no governance today.
- **P2P implementation methods:** WSDL, REST.
- **Documentation:** "Currently an area under development internally" — i.e. weak/immature.
- **Migration strategy:** Undecided — AIMO explicitly asks for our input on lift-and-shift vs. lift-with-adjustments vs. rebuild.
- **HA / SLA:** Not yet defined; AIMO does not believe they need geo-redundant HA.
- **Agents:** Undecided — Frends-hosted vs. AIMO's own infra/on-prem; open to recommendation.
- **API subscription:** Currently fully manual; AIMO wants a more efficient (commercial-product-native) workflow.
- **Execution volumes / peak traffic:** Not yet provided — AIMO said they're gathering these.

## PoC Pattern Inventory — Tier 1 (original 6, `WSO2 sample configs/`)

| ID | Sample | Pattern | Frends-relevant RFI section | Complexity | Risk (as AI-conversion test) | Readiness | Status |
|----|--------|---------|------------------------------|------------|-------------------------------|-----------|--------|
| SYN-150 | sample150 | Proxy service (API-fronted backend, publishes WSDL) | §5 API Management | Low | Low | **Implemented (PoC build)** | Complete |
| SYN-001 | sample1 | Content-based routing (XPath/regex filter on message property) | §3 Orchestration & Process Flow | Low | Low | Ready for Implementation | Planning |
| SYN-003 | sample3 | Reusable registry endpoint + sequence (named, referenced by key) | §11 Governance & Cataloguing | Medium | Medium — indirection is the likeliest naive-conversion trap | **Implemented (PoC build)** | Complete |
| SYN-004 | sample4 | Error handling (default `fault` sequence + named `onError` override) | §7 Error Handling & Resilience | Medium | Medium | Ready for Implementation | Planning |
| SYN-056 | sample56 | WSDL-resolved endpoint (service+port lookup vs. hardcoded URL) | §1 Connectivity & Protocol Support | Low | Low | Ready for Implementation | Planning |
| SYN-300 | sample300 | Scheduled polling task (timer-triggered message injection) | §4 Messaging & Event Handling / §3 Orchestration | Low | Low | Ready for Implementation | Planning |

*SYN-150 and SYN-003 were built in Frends on 2026-09-15 as POC1000/POC1001/POC1002 (see
`get_process_data`/`list_processes` in the Frends tenant) — a live demo proof point, not a
statement that the other four are blocked.*

## PoC Pattern Inventory — Tier 2 (complex, `WSO2 sample configs/complex/`)

| ID | Sample | Pattern | Frends-relevant RFI section | Complexity | Risk (as AI-conversion test) | Readiness | Status |
|----|--------|---------|------------------------------|------------|-------------------------------|-----------|--------|
| SYN-420 | sample420 | Cache mediator (check/collector pair, in-memory, bounded) | §1 Connectivity & Protocol Support | Low | Low — resolved, Shared State Task is native | **Ready With Assumptions** | Analysis |
| SYN-100 | sample100 | WS-Security policy binding (externally-governed security) | §1 / §7 | Medium | Medium — two-branch answer, native for common profile | Ready With Assumptions (Branch A only) | Analysis |
| SYN-350 | sample350 | Script mediator (embedded JavaScript, request+response hooks) | §3 / §11 | Medium | High — actual script content missing from corpus | **Not Ready — missing source** | Analysis |
| SYN-800 | sample800 | REST API, multiple resources (path/method-shaped, sync + fire-and-forget) | §5 API Management | Medium | Low — resolved, Intermediate Return is native | **Implemented (PoC build)** | Complete |
| SYN-360 | sample360 | DB-enriched routing (JDBC lookup feeding a switch) | §1 / §3 | Medium | Low–Medium | Ready With Assumptions | Analysis |
| SYN-400 | sample400 | Scatter-gather (iterate/aggregate, fan-out/fan-in) | §3 / §4 | High | Low — resolved (Foreach confirmed sequential-only, a known limitation not a blocker) + unresolved partial-failure design | **Implemented (PoC build)** | Complete |
| SYN-700 | sample700+702 | Message store + scheduled forwarding processor (store-and-forward) | §4 / §7 | High | Medium — ack mechanism resolved; still needs a chosen queue/broker (infrastructure, not design) | Not Ready | Analysis |
| SYN-055 | sample55(+53) | Session-affine load balancing over failover groups (4-level endpoint tree) | §1 / §7 | **Very High** | **High — confirmed genuine Frends capability gap, no native primitive** | Ready With Assumptions, **not recommended pending AIMO relevance check** | Analysis |
| SYN-156 | sample156 | Cross-sequence continuation + enrich-based 2-backend comparison | §3 / §11 | **Very High** | High — blocked on SYN-100's resolution | **Not Ready (blocked by SYN-100)** | Analysis |

**Note on "Risk" column:** since these are synthetic test patterns (no production behaviour to
break), risk here means *risk that the AI-assisted conversion mismodels the pattern, or that a real
Frends capability gap exists*, not business risk. Business criticality is not applicable — these
are not real integrations.

**Headline finding from Tier 2 (updated 2026-09-16 — all 5 open capability questions now checked
against docs.frends.com, not left as guesses):**

| Question | Pattern(s) | Resolution |
|---|---|---|
| Cache store mechanism | SYN-420 | **Native** — Shared State Task (TTL 1min–30 days) |
| Fire-and-forget / detached Return | SYN-800, SYN-700 | **Native** — Intermediate Return |
| Foreach concurrency | SYN-400 | **Confirmed sequential-only** — real but minor limitation |
| WS-Security | SYN-100, SYN-156 | **Native for UsernameToken+Timestamp; not for full policy-driven signing/encryption** |
| Load-balance/failover endpoint composition | SYN-055 | **Confirmed gap — no native primitive** |

4 of 5 resolved in Frends' favor (3 fully native, 1 workable-with-a-known-limitation); 1
(load-balance/failover) is a confirmed real gap needing a custom build, and its actual relevance to
AIMO's real estate is still unconfirmed (their production backends are mostly SaaS, which manage
their own HA). This is directly useful evidence for the RFI: it shows AI-assisted conversion
correctly flagging "stop and check the platform first" rather than confidently guessing wrong, and
the follow-up checks resolved most of those flags favorably rather than surfacing a wall of gaps.

## Migration Sequencing (for the PoC exercise itself)

Following the READMEs' own suggested testing order — simplest/most self-contained first, then
increasing structural difficulty, tier by tier:

**Tier 1 (done/in progress):**
1. **SYN-150** (proxy) — ✅ built. Shortest, most self-contained; sanity-checked that AI conversion produces a sane Frends process shape at all.
2. **SYN-001** (content-based routing) — first step up: conditional branching. Documented, not built.
3. **SYN-003** (registry reuse) — ✅ built (as POC1001+POC1002). Tests whether indirection is correctly recognized. Most relevant to AIMO's stated governance gap (§11).
4. **SYN-004** (error handling) — documented, not built.
5. **SYN-056** (WSDL endpoint) and **SYN-300** (scheduled task) — documented, not built.

**Tier 2 (complex — analysis complete, none built):**
1. **SYN-420** (cache) and **SYN-100** (WS-Security) — lowest complexity in this tier; SYN-100
   resolved via a real docs.frends.com lookup into a two-branch answer.
2. **SYN-350** (script mediator) — blocked on missing script source, not a Frends question.
3. **SYN-800** (REST API multi-resource) — API-shape modeling; blocked on confirming the
   detached-Return mechanism.
4. **SYN-360** (DB lookup) — external DB state feeding a (simplified) decision.
5. **SYN-400** (scatter-gather) — fan-out/fan-in; blocked on Foreach concurrency confirmation and
   an explicit partial-failure design decision.
6. **SYN-700** (message store/processor) — async store-and-forward; blocked on a queue/broker
   infrastructure choice.
7. **SYN-055** (load-balance/failover) — deepest endpoint nesting; likely a genuine capability gap.
8. **SYN-156** (enrich/multi-sequence) — most tangled; hard-blocked on SYN-100's resolution.

## Migration Metrics

- Total PoC patterns: 15 (6 Tier 1 + 9 Tier 2)
- Source-analysis complete: 15 / 15
- Migration plans complete: 15 / 15 (all drafted — several explicitly land on NOT READY, which is
  itself a completed, useful assessment, not a gap)
- Ready for implementation: 4 / 15 (SYN-001, SYN-004, SYN-056, SYN-300 — Tier 1 remainder)
- Ready with assumptions: 6 / 15 (SYN-100 Branch A only, SYN-360, SYN-420, SYN-800, SYN-400,
  SYN-055 — the last not recommended pending an AIMO relevance check, per its plan)
- Not ready (blocked on missing source or an infrastructure decision — no longer any pure
  unresolved-capability blockers after the 2026-09-16 capability-check pass): 2 / 15 (SYN-350
  missing script source, SYN-700 pending queue/broker choice) + SYN-156 blocked transitively via
  SYN-100's WS-Security branch
- Approved for build: 4 / 15 (SYN-150, SYN-003 — 2026-09-15; SYN-800, SYN-400 — 2026-09-16)
- Implemented in Frends: 4 / 15 (POC1000; POC1001+POC1002; POC1003+POC1004; POC1005 v0.0.2)
- Reusable components implemented (not just candidates): 1 — `SUB-DefaultErrorHandler` (POC1006),
  attached to POC1005 on 2026-09-17. POC1000/1001/1002/1003/1004 do not yet have a process-level
  error handler — flagged as a retrofit opportunity, not yet actioned (out of scope of what was
  asked).
- Real AIMO integrations inventoried: 0 / 354 (126 WSO2 + 228 P2P) — **blocked**, no real exports shared yet

## Open Questions / Blockers (carry into next AIMO touchpoint)

**Real-migration blockers (unchanged since Tier 1):**
1. AIMO has not shared any real WSO2/Synapse config, P2P integration code, or documentation — the entire real migration (Discover→Inventory phases) is blocked on this.
2. Execution volumes and peak traffic figures — AIMO said these were being gathered "later this week" (per Q&A doc; exact date not stated).
3. Whether the 354 integration count is final scope or includes to-be-retired items — AIMO explicitly wants our input to decide.
4. Migration strategy choice (lift-and-shift vs. lift-with-adjustments vs. rebuild) — AIMO wants our recommendation, not yet decided.
5. Agent hosting model (Frends-hosted vs. private/on-prem) — open, pending security evaluation.
6. HA/SLA requirements — undefined; needs a follow-up definition workshop.
7. Whether external client API calls are in-scope/counted within the 126+228 figure — Q&A says "very few, can be included," but exact endpoint count not given.
8. The two docs the corpus READMEs reference (`Aimo Park RFI - Frends PS Analysis.md`,
   `WSO2 to Frends Translation Analysis.md`) still do not exist in the SharePoint folder as of
   2026-09-16 — confirmed via a direct parent-folder listing, not assumed.

**Frends capability questions surfaced by Tier 2 — all checked against docs.frends.com on
2026-09-16 (not memory); resolutions below, not open anymore except where noted:**
9. **Cache backing mechanism** (SYN-420) — ✅ **Resolved: native Shared State Task** (TTL
   1min–30 days). Remaining sub-item: source's 20s TTL is below Shared State's 1-minute minimum
   granularity — a design rounding, not a capability gap.
10. **WS-SecurityPolicy signing/encryption** (SYN-100, SYN-156) — ✅ **Resolved as a two-branch
    answer:** native for UsernameToken+Timestamp via `Frends.Soap.Request`; genuinely unsupported
    (custom build via `SignXml`) for full policy-driven signing/encryption. Which branch applies to
    SYN-100/SYN-156 still depends on the real (unavailable) policy document.
11. **Detached Return / fire-and-forget** (SYN-800, SYN-700) — ✅ **Resolved: native Intermediate
    Return** shape genuinely detaches the caller from subsequent shapes.
12. **Foreach concurrency model** (SYN-400) — ✅ **Resolved: confirmed sequential-only.** True
    parallelism needs design-time-fixed parallel branches, not a dynamic fan-out — a real,
    documented performance limitation, not a blocker.
13. **Load-balance/failover endpoint composition** (SYN-055) — ✅ **Resolved: confirmed genuine
    gap, no native primitive.** Still open: whether AIMO's real estate (mostly SaaS backends) even
    needs this pattern — a relevance question, not a capability one, and the reason this pattern
    stays "not recommended" rather than simply "ready."
14. **`SYN-350`'s actual script content** was never provided in the corpus — still a missing-source
    problem, not a Frends question; unaffected by this capability-check pass.

**Net result of this pass:** every genuine open Frends-capability question from Tier 2 is now
resolved one way or the other. What remains open is either missing source material (SYN-350),
an infrastructure choice (SYN-700's queue/broker), a real-world-relevance question (SYN-055), or a
transitively-inherited block (SYN-156 ← SYN-100).

## Approval Gate

**Nothing here is a candidate for Frends MCP build.** These are illustrative conversions of public
sample code for an RFI PoC exhibit, not integrations AIMO has asked to be migrated. Per the standard
hard gate, no Frends process will be created via MCP tools without explicit user approval — and in
this case, also not without AIMO's own real integration assets to migrate.
