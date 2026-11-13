# SYN-056 — Migration Plan: WSDL-Resolved Endpoint → Environment Variable

**Corpus note:** based on public Synapse sample #56, not real AIMO configuration.

## 1. Integration Overview

Replace WSDL-based endpoint resolution with a build-time-resolved Environment Variable. Demonstrates
RFI §1 Connectivity & Protocol Support.

## 2. Source and Target Systems

Source: inbound message. Target: `SimpleStockQuoteService`, address resolved from WSDL.

## 3. Trigger

Frends HTTP Trigger (assumed).

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture

Functionally identical to SYN-150/SYN-003's HTTP.Request pattern: Trigger → HTTP.Request Task
(URL from `ENV-StockQuoteServiceUrl`, resolved once from the WSDL at build time, not dynamically per
call) → Return.

## 6. Data Mapping

See `mapping.md`.

## 7. Business Rules

None.

## 8. Error Handling

Recommend the standard default error handler (SYN-004 pattern).

## 9. Logging and Monitoring

Not present in source; recommend standard request logging.

## 10. Security

Not addressed in source.

## 11. Dependencies

`ENV-StockQuoteServiceUrl` (shared candidate — same environment variable as SYN-150/SYN-003, since
all three point at the same backend in this corpus).

## 12. Flow Diagram

See `flow.mmd`.

## 13. Frends Process Breakdown

Trigger → HTTP.Request (using `ENV-StockQuoteServiceUrl`) → Return → default error handler.

## 14. Migration Considerations

**Key architectural difference:** Synapse resolves the endpoint from a WSDL file at runtime
startup; Frends has no direct equivalent, so the proposed design resolves it once (at
integration-build time, by reading the WSDL manually) and stores the result as a plain Environment
Variable. This is safe **only if** the real-world use is static resolution, not dynamic service
discovery — see Open Questions.

## 15. Testing Strategy

Functional: confirm the resolved URL matches what the WSDL declares for the named service+port;
confirm requests reach the correct endpoint.

## 16. Open Questions

1. Does any real AIMO integration use WSDL-based resolution for genuine dynamic service discovery
   (e.g., picking between multiple ports/environments at runtime), rather than a one-time static
   lookup? If so, the Environment Variable approach is insufficient and needs a different design.

## 17. Assumptions

Assumed static, build-time WSDL resolution is representative; not yet confirmed for AIMO's real
estate.

## 18. Reusable Components (existing)

Reuses `ENV-StockQuoteServiceUrl` (originated in SYN-003's plan).

## 19. New Reusable Components (candidates)

None new.

## 20. Readiness Assessment

**READY WITH ASSUMPTIONS** for the PoC conversion exercise. Not a real AIMO integration — no build
without explicit approval.
