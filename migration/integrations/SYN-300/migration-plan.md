# SYN-300 — Migration Plan: Scheduled Polling Task → Frends Schedule Trigger

**Corpus note:** based on public Synapse sample #300, not real AIMO configuration.

## 1. Integration Overview

Replace Synapse's timer-based `MessageInjector` task with a native Frends **Schedule Trigger**.
Demonstrates RFI §4 Messaging & Event Handling / §3 Orchestration — the only time-triggered pattern
in this corpus.

## 2. Source and Target Systems

Source: internal timer. Target: `SimpleStockQuoteService` (PoC).

## 3. Trigger

Frends **Schedule Trigger** — direct replacement for `<task><trigger interval="5"/></task>`.

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture

1. Schedule Trigger fires on a realistic interval (see Migration Considerations — 5s is a demo
   value, not to be carried forward literally).
2. Build the request payload (static in this sample; a Code/Template element if parameterization
   is ever needed).
3. `HTTP.Request` Task → backend.
4. Extract response fields via expression, log them.

## 6. Data Mapping

See `mapping.md`.

## 7. Business Rules

None beyond "poll on a fixed interval with a fixed payload."

## 8. Error Handling

Not modeled in source. Recommend standard default error handler — particularly important here
since a failed poll on a tight interval could otherwise flood logs/retries without one.

## 9. Logging and Monitoring

Preserve the three-field extraction-and-log behaviour from the source.

## 10. Security

Not addressed in source.

## 11. Dependencies

`SUB-StockQuoteClient` / `ENV-StockQuoteServiceUrl` (shared candidates).

## 12. Flow Diagram

See `flow.mmd`.

## 13. Frends Process Breakdown

Schedule Trigger → build payload → HTTP.Request → extract 3 fields → Log → default error handler.

## 14. Migration Considerations

- **Interval:** the source's 5-second interval is a demo-scale value from the public sample, not a
  realistic production cadence. Any real AIMO scheduled integration needs its actual required
  cadence captured, not this literal value.
- **Payload:** the source's static `IBM` payload doesn't exercise parameterization at all; flagged
  as an assumption in source-analysis.md.

## 15. Testing Strategy

Functional: confirm the trigger fires on schedule, the request reaches the backend, and the three
log fields are correctly extracted from a sample response.

## 16. Open Questions

1. What is a realistic polling interval for AIMO's real scheduled integrations (RFI Q&A confirms
   "many are scheduled" but gives no cadence)?
2. Do any of AIMO's real scheduled integrations need a dynamic/parameterized payload per poll,
   rather than a static one?

## 17. Assumptions

Assumed the 5-second interval and static payload are demo artifacts, not requirements to replicate.

## 18. Reusable Components (existing)

Reuses `SUB-StockQuoteClient` / `ENV-StockQuoteServiceUrl` if built first per suggested sequencing.

## 19. New Reusable Components (candidates)

None new.

## 20. Readiness Assessment

**READY WITH ASSUMPTIONS** for the PoC conversion exercise. Not a real AIMO integration — no build
without explicit approval.
