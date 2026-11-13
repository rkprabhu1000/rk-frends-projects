# Reusable Components — AIMO Park WSO2 → Frends PoC

Catalogue of Frends-side reusable components identified while designing equivalents for the 6
Synapse PoC patterns. None of these exist in Frends yet — all are **candidates**, to be built only
once a real integration is approved for implementation (see dashboard approval gate).

## Candidate: `SUB-StockQuoteClient` (subprocess)

- **Purpose:** Encapsulate the SOAP call to the shared backend (`SimpleStockQuoteService` in the
  PoC; would be a real AIMO backend — e.g. NetSuite — in production) behind one subprocess, so every
  calling process invokes it the same way instead of re-implementing the HTTP.Request call.
- **Inputs:** stock symbol (PoC) / real request payload; target URL (via Environment Variable).
- **Outputs:** parsed response (quote fields in the PoC).
- **Dependencies:** `ENV-StockQuoteServiceUrl` (environment variable, see below).
- **Where it would be used:** SYN-150, SYN-001, SYN-003, SYN-004, SYN-056, SYN-300 — i.e. every PoC
  sample, since they all call the same backend. This is the direct Frends-native analogue of
  Synapse's `<endpoint name="simple">` + `<sequence name="stockquote">` in SYN-003.
- **Implementation status:** Not built. Candidate only.
- **Version/compatibility:** N/A.

## Candidate: `ENV-StockQuoteServiceUrl` (environment variable)

- **Purpose:** Externalized backend URL, replacing every hardcoded
  `http://localhost:9000/services/SimpleStockQuoteService` across the 6 samples.
- **Dependencies:** none.
- **Where it would be used:** same 6 samples as `SUB-StockQuoteClient`.
- **Implementation status:** Not built. Candidate only.

## Component: `SUB-DefaultErrorHandler` (subprocess / process-level error handler) — **IMPLEMENTED**

- **Purpose:** Frends-native equivalent of Synapse's default `fault` sequence — identifies which
  process failed and terminates gracefully. Attached as the default process error handler;
  branches needing a custom override (like SYN-004's `sunErrorHandler`) get a scoped `Catch`
  instead of using this default.
- **Inputs:** `ProcessName` (text) — the name of the failing process, passed by the caller at
  attach time via `process_set_error_handler`'s `parameters`.
- **Outputs:** a summary string identifying the failed process and timestamp.
- **Dependencies:** none.
- **Where it's used:** `POC1005: Stock quote scatter-gather` (attached 2026-09-17). Not yet
  attached to POC1000, POC1001/1002, POC1003, or POC1004 — those were built before this component
  existed and currently have no process-level error handler either. Worth retrofitting if/when
  requested.
- **Implementation status:** **Built** — `POC1006: Default error handler`,
  GUID `9faa582b-fa00-49ab-9108-2df573aea2ef`, deployed to Default/Default01.
- **Known limitation:** the platform-documented `#var.error` reference (exception
  message/type/stack trace) did not resolve when this subprocess was built purely via MCP tools —
  compile error `frends_variable_error does not exist in the current context`. Appears to require
  the portal UI's error-handler designation flow to wire up, not the API alone. Currently the
  handler identifies which process failed but not the specific exception detail; operators need
  the execution's step log for that. Flagged for follow-up, not silently worked around.

## Candidate: `SUB-SoapSecureClient` (subprocess)

- **Purpose:** Wrapper around `Frends.Soap.Request` with WS-Security UsernameToken+Timestamp
  pre-wired to an Environment Variable pattern, for any integration needing Branch A security
  (per SYN-100's findings). Does **not** cover policy-driven signing/encryption (Branch B) — that
  is explicitly out of scope for this component.
- **Inputs:** SOAP request body, target URL, WS-Security username/password (from secret env vars).
- **Outputs:** parsed SOAP response.
- **Dependencies:** `Frends.Soap.Request` Task (needs importing — not yet confirmed present in the
  tenant).
- **Where it would be used:** SYN-100, SYN-156 (secure backend leg) — contingent on confirming
  Branch A actually applies once a real WS-Security policy is known.
- **Implementation status:** Not built. Candidate only.

## Candidate: `SUB-CacheLookup` (subprocess)

- **Purpose:** Generic "compute key → check cache → return hit/miss" component for the
  cache-mediator pattern (SYN-420).
- **Dependencies:** **Resolved (2026-09-16)** — the native **Shared State Task**
  (`process_add_shared_state`), TTL 1 minute–30 days.
- **Where it would be used:** SYN-420, and any other AIMO integration found to need
  request/response caching, or session-affinity state (SYN-055 also now depends on Shared State
  for a different purpose — session-pinning — so this component and SYN-055's needs may share
  underlying Shared State usage patterns worth consolidating).
- **Implementation status:** Not built. Candidate only — no longer blocked on an open capability
  question, just on being prioritized for an actual build.

## Candidate: `SUB-DbLookupWithFallback` (subprocess)

- **Purpose:** Generic "parameterized DB lookup → found (continue) or not-found (fault)" component
  for the DB-enriched-routing pattern (SYN-360).
- **Dependencies:** a SQL/DB connectivity Task — not yet confirmed available/imported in the
  tenant.
- **Where it would be used:** SYN-360, and any AIMO integration with similar DB-gated routing.
- **Implementation status:** Not built. Candidate only.

## Candidate: `SUB-GuaranteedDeliveryForwarder` (subprocess)

- **Purpose:** Generic "poll queue, forward, retry/dead-letter on failure" component for the
  store-and-forward pattern (SYN-700).
- **Dependencies:** a real external queue/broker (infrastructure prerequisite, not yet chosen —
  see SYN-700's Open Question #1). Note: the *ack-the-caller* half of this component's calling
  process is resolved (Intermediate Return, confirmed 2026-09-16) — only the queue/broker choice
  remains open.
- **Where it would be used:** SYN-700, and any AIMO integration needing guaranteed/async delivery.
- **Implementation status:** Not built. Candidate only — blocked on a queue/broker technology
  decision (infrastructure, not design).

## Candidate: `SUB-EndpointGroupSelector` (subprocess)

- **Purpose:** Generic "session-pin to a group, try group members with failover, hand-rolled
  load-balance across groups" component for the load-balance/failover pattern (SYN-055) — the one
  pattern in this catalogue with **no native Frends primitive** behind it.
- **Dependencies:** Shared State Task (for session-pinning persistence, confirmed native), plus
  fully custom Decision/Catch orchestration logic (confirmed no shortcut exists).
- **Where it would be used:** SYN-055 only, so far — and only if AIMO's real estate is confirmed to
  actually need this pattern (see SYN-055's Open Question #4). Do not build speculatively.
- **Implementation status:** Not built. Candidate only, and lowest priority of all candidates in
  this catalogue pending real-world relevance confirmation.

## Governance note

AIMO's RFI explicitly flags "no structures for re-use" today (§11) as a gap in their current WSO2
setup. These three candidates are the PoC-scale illustration of what a Frends reusable-component
catalogue would look like for their real estate — worth surfacing directly in the RFI response as
evidence of what governance improvement looks like, not just faster migration.
