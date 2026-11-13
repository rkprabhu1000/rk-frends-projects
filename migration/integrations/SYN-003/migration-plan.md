# SYN-003 — Migration Plan: Registry Reuse → Frends Subprocess + Environment Variables

**Corpus note:** based on public Synapse sample #3, not real AIMO configuration. This is the
architecturally most important sample in the corpus — see source-analysis.md.

## 1. Integration Overview

Preserve Synapse's key-based indirection (named endpoint + named sequence, referenced rather than
inlined) as genuine Frends reuse constructs. Directly demonstrates RFI §11 Governance &
Cataloguing — the gap AIMO explicitly says it has today.

## 2. Source and Target Systems

Source: inbound message. Target: `SimpleStockQuoteService` (PoC), resolved via the reusable
endpoint.

## 3. Trigger

Frends HTTP Trigger (assumed; not confirmed — same caveat as SYN-001).

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture

1. Trigger receives message, sets a `direction` context value.
2. **Call Activity** invokes `SUB-StockQuoteClient` (the Frends-native equivalent of the reusable
   `stockquote` sequence) — this is the key design decision: reuse must be a real subprocess
   boundary, not an inlined copy.
3. Inside the subprocess: log statement referencing `ENV-Version` and the passed-in `direction`
   value.
4. `HTTP.Request` Task calls the backend URL from `ENV-StockQuoteServiceUrl` (Frends-native
   equivalent of the `simple` endpoint registry entry).
5. Response returned to caller.

## 6. Data Mapping

See `mapping.md`.

## 7. Business Rules

None beyond the reuse/indirection structure itself.

## 8. Error Handling

Not modeled in source. Recommend the standard default error handler (SYN-004 pattern).

## 9. Logging and Monitoring

Source already includes a custom log statement; preserve it, resolving `version`/`direction`
from Frends Environment Variables / process variables rather than literal values.

## 10. Security

Not addressed in source.

## 11. Dependencies

- `SUB-StockQuoteClient` (candidate subprocess — **this plan is the one that defines it**, other
  plans in this corpus reuse it).
- `ENV-StockQuoteServiceUrl`, `ENV-Version` (candidate environment variables).

## 12. Flow Diagram

See `flow.mmd`.

## 13. Frends Process Breakdown

Calling process: Trigger → set `direction` → Call Activity (`SUB-StockQuoteClient`) → Return.
Subprocess `SUB-StockQuoteClient`: Log (version + direction) → HTTP.Request → Return to caller.

## 14. Migration Considerations

**This is the key difference from a naive conversion:** a line-by-line translation would inline the
`simple` endpoint URL and the `stockquote` logic directly into the calling process, discarding
reuse. The proposed design instead makes `SUB-StockQuoteClient` a real, independently-versioned
subprocess and `ENV-StockQuoteServiceUrl`/`ENV-Version` real environment variables — so if/when
AIMO's real integrations share this same backend across multiple WSO2 flows, the Frends equivalent
is genuinely shared too, not duplicated per-integration.

## 15. Testing Strategy

- Functional: confirm the subprocess is invoked (not inlined) and returns the expected response.
- Confirm `ENV-Version`/`ENV-StockQuoteServiceUrl` changes propagate to all callers without
  redeploying each calling process individually — the actual governance benefit being tested.

## 16. Open Questions

1. Once real AIMO WSO2 assets are available: which of their 126 integrations actually share
   endpoints/sequences via WSO2's registry the way this sample does? This determines how large the
   real reusable-component catalogue needs to be.

## 17. Assumptions

Assumed a 1:1 mapping of "Synapse registry entry" → "Frends Environment Variable" and "Synapse
reusable sequence" → "Frends subprocess." This is the most defensible mapping but not yet validated
against Frends' own governance/cataloguing tooling for subprocess versioning at scale.

## 18. Reusable Components (existing)

None — this plan originates `SUB-StockQuoteClient`, `ENV-StockQuoteServiceUrl`, `ENV-Version`.

## 19. New Reusable Components (candidates)

- `SUB-StockQuoteClient` (subprocess)
- `ENV-StockQuoteServiceUrl` (environment variable)
- `ENV-Version` (environment variable)

All added to `architecture/reusable-components.md`.

## 20. Readiness Assessment

**READY WITH ASSUMPTIONS** for the PoC conversion exercise — this is the plan to prioritize
building first among the 6 if/when a build is approved, since the other 5 plans' reusable
components depend on decisions made here. Still not a real AIMO integration — no build without
explicit approval.
