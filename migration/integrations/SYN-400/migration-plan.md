# SYN-400 — Migration Plan: Scatter-Gather → Foreach + Collect

**Corpus note:** based on public Synapse sample #400, not real AIMO configuration.

## 1. Integration Overview

Split one inbound message into N backend calls, then recombine all N responses. Demonstrates RFI
§3/§4 — no flat/linear Frends equivalent exists; this needs `Foreach`.

## 2. Source and Target Systems

Source: inbound message with N repeating request elements. Target: `SimpleStockQuoteService`,
called once per element.

## 3. Trigger

Frends HTTP Trigger (assumed).

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture

1. Trigger receives the message.
2. Parse the repeating element into a list (Code element or expression).
3. **`Foreach`** over the list: each iteration runs an `HTTP.Request` Task against the backend.
4. Each iteration's result is collected (per Frends' native Foreach result-collection behaviour —
   see Open Questions for confirming exact mechanics).
5. After the loop: assemble the collected results into one combined response.
6. Return.

## 6. Data Mapping

See `mapping.md`.

## 7. Business Rules

None beyond "process every element, then combine."

## 8. Error Handling

**Explicitly decided here, since the source leaves it unspecified:** recommend that a failure in
any single iteration should not silently corrupt the aggregate — either (a) fail the whole process
on any iteration failure (simplest, safest default), or (b) collect per-item success/failure and
report both in the final result. Recommend (a) as the default unless AIMO's real use case needs
partial-success semantics — flag as an explicit design decision needing sign-off, not an inherited
assumption.

**Implemented 2026-09-17:** option (a) is now live in POC1005 (v0.0.2). Two changes were needed:
1. `Frends.HTTP.Request`'s `ThrowExceptionOnErrorResponse` was `false` at initial build —
   meaning a non-2xx backend response would have been silently appended to the results list as if
   successful, not treated as a failure at all. Corrected to `true`.
2. A process-level default error handler (`POC1006: Default error handler`, the
   `SUB-DefaultErrorHandler` candidate from `architecture/reusable-components.md`, now actually
   built) is attached via `process_set_error_handler`. Any unhandled exception — including the
   HTTP failure surfaced by fix #1 — now fails the whole process cleanly instead of silently
   corrupting the aggregate.

**Known tooling limitation, documented rather than hidden:** the Frends reference `#var.error`
(exception message/type/stack trace, supposedly "always available inside error-handling
subprocesses" per platform guidance) did not resolve when the handler subprocess was built purely
via MCP tools — compile error `frends_variable_error does not exist in the current context`. It
appears this variable is wired up by the portal UI's error-handler designation flow, not by the
API-level `process_set_error_handler` call alone. Worked around by having the handler identify
*which process* failed (via a passed `ProcessName` parameter) and pointing operators to the
execution's step log for the actual exception detail, rather than fabricating exception content
that isn't actually available. This should be revisited — e.g. confirm via manual portal
inspection whether the deployed handler actually has `#var.error` available at runtime despite the
compile-time issue.

## 9. Logging and Monitoring

Recommend logging the split count (N) and, on completion, how many succeeded/failed — visibility
the source sample doesn't have at all.

## 10. Security

Not addressed in source.

## 11. Dependencies

`SUB-StockQuoteClient`/`ENV-StockQuoteServiceUrl` if reused inside the Foreach body.

## 12. Flow Diagram

See `flow.mmd`.

## 13. Frends Process Breakdown

Trigger → parse list → Foreach [HTTP.Request → collect result] → aggregate collected results →
Return.

## 14. Migration Considerations

**Resolved via docs.frends.com (2026-09-16):** Frends' `Foreach` is confirmed sequential-only.
Synapse's `<iterate>` implies concurrent-capable dispatch — the *correctness* of the port is
unaffected (same aggregated result) but the *performance characteristic* is genuinely different: N
sequential HTTP calls instead of N potentially-parallel ones. There is no native way to get a
dynamically-sized parallel fan-out (only design-time-fixed parallel branches). If N is large or
latency-sensitive in any real AIMO use case using this pattern, this is a real, confirmed
limitation to flag — not solvable by picking a different shape, only by a fundamentally different
(and more custom) design, e.g. spinning up N Call Activities via a fixed set of parallel branches
if N has a known upper bound, or accepting the sequential latency.

## 15. Testing Strategy

- N=1: trivial case, confirm single-item round-trip.
- N>1: confirm all N backend calls happen and all N responses are correctly recombined in a stable
  order.
- One backend call fails mid-batch: confirm the decided error-handling behaviour (§8) actually
  happens, not an inherited "unspecified" behaviour.

## 16. Open Questions

1. **Resolved:** Frends `Foreach` is sequential-only, confirmed via docs.frends.com. This is now a
   known limitation to design around, not an unknown.
2. Partial-failure semantics (§8) — needs an explicit decision, not an assumption.
3. Does AIMO's real estate have any genuine fan-out/fan-in integrations, or is this purely a PoC
   pattern test with no real-world counterpart yet identified? If any real use case is
   latency-sensitive with large N, the sequential-only limitation (§14) becomes materially
   important to surface to AIMO early.

## 17. Assumptions

Assumed "fail the whole batch on any error" as the safer default pending confirmation.

## 18. Reusable Components (existing)

Would reuse `SUB-StockQuoteClient`/`ENV-StockQuoteServiceUrl` inside the loop body.

## 19. New Reusable Components (candidates)

None new — the Foreach + collect pattern itself is generic enough to document in
`architecture/shared-patterns.md` as a reusable *pattern* (not a reusable *component*) once
confirmed.

## 20. Readiness Assessment

**IMPLEMENTED (PoC build), with error handling completed 2026-09-17.** The former blocker
(Foreach concurrency model) is resolved (sequential-only, documented limitation); the
partial-failure decision (§2) is now implemented as "fail the whole batch," not just recommended.
Remaining item: confirm at runtime (via the portal) whether `#var.error` is actually populated in
`POC1006`'s execution despite not resolving at compile time through MCP tools alone. Not a real
AIMO integration regardless.
