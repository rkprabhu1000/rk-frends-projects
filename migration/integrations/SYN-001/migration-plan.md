# SYN-001 — Migration Plan: Content-Based Routing → Frends Process

**Corpus note:** based on public Synapse sample #1, not real AIMO configuration.

## 1. Integration Overview

Route inbound messages to a backend call only when a destination property matches a pattern;
otherwise pass through unchanged. Demonstrates RFI §3 (Orchestration & Process Flow) — Synapse's
`<filter>` maps directly onto Frends' `ExclusiveGateway`.

## 2. Source and Target Systems

Source: inbound message (any caller). Target: `SimpleStockQuoteService` (PoC) when matched.

## 3. Trigger

Frends **HTTP Trigger** (or whatever real trigger fronts this in AIMO's actual usage — unknown,
flagged below).

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture

1. Trigger receives the message.
2. `ExclusiveGateway` evaluates the routed field/property against the match pattern
   (per [[feedback_frends_gateways]], the expression lives on the gateway itself).
3. Matched branch → `HTTP.Request` Task → backend, then Return/terminate.
4. Unmatched branch → default passthrough Return.

## 6. Data Mapping

See `mapping.md` — routing decision only, no field transformation.

## 7. Business Rules

Single rule: forward only messages whose destination matches `.*/StockQuote.*`; all else passes
through unchanged (default branch).

## 8. Error Handling

Not modeled in the source sample. Recommend the standard process-level default error handler (see
SYN-004 pattern) regardless of source having none.

## 9. Logging and Monitoring

Not present in source. Recommend adding a log entry recording which branch was taken, for
operational visibility (an improvement over the source, documented per Continuous Traceability).

## 10. Security

Not addressed in source sample.

## 11. Dependencies

`SUB-StockQuoteClient`, `ENV-StockQuoteServiceUrl` (candidates, shared with SYN-150 — see
`architecture/reusable-components.md`).

## 12. Flow Diagram

See `flow.mmd`.

## 13. Frends Process Breakdown

1. Trigger.
2. ExclusiveGateway (match on routed property).
3. Branch A: HTTP.Request → Return.
4. Branch B (default): Return passthrough.
5. Process error handler.

## 14. Migration Considerations

No behavioural change proposed beyond adding logging (§9) and a default error handler that the
source lacked — both additive, non-breaking improvements per Frends Development Principles (design
for operations).

## 15. Testing Strategy

- Functional: message with matching `To` → confirm backend call occurs.
- Functional: message with non-matching `To` → confirm default passthrough, no backend call.
- Negative: backend unreachable on matched branch → confirm error handler fires.

## 16. Open Questions

1. Is "route by destination property" a shared front-door pattern used across multiple real AIMO
   WSO2 integrations, or a one-off? Materially affects whether this becomes a shared Frends
   component or a one-off gateway per integration.
2. What real trigger type fronts this in AIMO's actual usage (HTTP, queue, something else)? Unknown
   from this sample.

## 17. Assumptions

Assumed HTTP as the real-world trigger type, since `To` reads naturally as a URI/destination in an
HTTP context; not confirmed.

## 18. Reusable Components (existing)

None built yet. Would reuse `SUB-StockQuoteClient` from SYN-150 if built first per the suggested
sequencing.

## 19. New Reusable Components (candidates)

None new beyond those already proposed under SYN-150.

## 20. Readiness Assessment

**READY WITH ASSUMPTIONS** for the PoC conversion exercise. Not a real AIMO integration — no build
should occur against this plan.
