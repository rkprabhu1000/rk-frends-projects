# SYN-150 — Migration Plan: Proxy Service → Frends Process

**Corpus note:** based on public Synapse sample #150, not real AIMO configuration. See
migration-dashboard.md scope framing.

## 1. Integration Overview

Expose a stable, callable API in Frends that fronts a backend SOAP service — direct analogue of a
Synapse proxy service. Demonstrates RFI §5 (API Management): Frends replaces "proxy + published
WSDL" with an HTTP-triggered Process plus Frends' own API Management/documentation.

## 2. Source and Target Systems

- Source: external SOAP client (any caller).
- Target: `SimpleStockQuoteService` (PoC) — in a real AIMO integration, whichever backend this
  proxy fronts (e.g. NetSuite).

## 3. Trigger

Frends **HTTP Trigger**, replacing the Synapse `<proxy>` inbound listener.

## 4. Current Integration Flow

See `source-analysis.md` — passthrough proxy with WSDL publication, no mediation logic.

## 5. Proposed Frends Architecture

A single Frends Process:
1. HTTP Trigger receives the inbound request.
2. `HTTP.Request` Task calls the backend (`SUB-StockQuoteClient` subprocess if that reusable
   component exists by build time — see `architecture/reusable-components.md`).
3. Response returned to caller via Return shape.
4. Process-level error handler for failures (see SYN-004 for the fuller error-handling pattern).

## 6. Data Mapping

See `mapping.md` — passthrough, no transformation in this sample.

## 7. Business Rules

None present in this sample.

## 8. Error Handling

Not modeled in this sample (Synapse relies on its process-wide default fault sequence, not shown
here). Recommend attaching the process-level default error handler pattern from SYN-004 regardless.

## 9. Logging and Monitoring

Not present in the source sample. Recommend adding a correlation-ID-bearing log entry per request
(consistent with Frends Development Principles — design for operations) even though the source
didn't have one; this is an improvement, not a required behaviour match.

## 10. Security

Not addressed in the source sample (no auth on the proxy). Real AIMO equivalents would need auth
requirements captured once known.

## 11. Dependencies

`SUB-StockQuoteClient`, `ENV-StockQuoteServiceUrl` (both candidates — see `reusable-components.md`).

## 12. Flow Diagram

See `flow.mmd`.

## 13. Frends Process Breakdown

1. HTTP Trigger (method/path TBD once real contract known).
2. HTTP.Request Task → backend.
3. Return shape → response body back to caller.
4. Process error handler → generic failure response.

## 14. Migration Considerations

- **Difference from source:** Synapse publishes a standalone WSDL as the proxy's contract. Frends
  does not have a direct equivalent primitive; the proposed replacement is Frends' own API
  documentation/management surface. This is a deliberate architectural change, not a gap — see
  Open Questions.
- Adds logging that the source sample lacked (see §9) — an intentional improvement, documented here
  per the Continuous Traceability requirement so it's not a silent behaviour change.

## 15. Testing Strategy

- Functional: send a request matching the PoC's `getQuote` shape, confirm passthrough response
  matches backend's raw response.
- Negative: backend unreachable → confirm error handler fires and returns a sane error, not a raw
  exception.

## 16. Open Questions

1. Does AIMO's real use of this pattern require the caller-facing WSDL/contract to be independently
   published (contract-first), or is Frends' own API documentation an acceptable replacement?
2. What authentication does the real backend/caller relationship require? Not modeled in the PoC.

## 17. Assumptions

- Assumed the "outSequence" passthrough in the sample is representative of at least some of AIMO's
  simpler API-fronting integrations; more complex ones almost certainly transform data and would
  need their own analysis.

## 18. Reusable Components (existing)

None yet exist — this is the first plan proposing `SUB-StockQuoteClient` / `ENV-StockQuoteServiceUrl`
as candidates.

## 19. New Reusable Components (candidates)

- `SUB-StockQuoteClient` (subprocess) — see `architecture/reusable-components.md`.
- `ENV-StockQuoteServiceUrl` (environment variable) — see same.

## 20. Readiness Assessment

**READY WITH ASSUMPTIONS.** The pattern conversion (proxy → HTTP-triggered process) is
architecturally sound and low-risk, but this is a PoC pattern test on public sample data — it is
**not** a real AIMO integration awaiting approval. No build should occur against this plan; it
exists to validate the conversion approach for the RFI PoC exhibit.
