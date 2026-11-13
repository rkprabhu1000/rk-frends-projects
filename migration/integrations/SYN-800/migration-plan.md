# SYN-800 — Migration Plan: REST API Multi-Resource → Two Frends HTTP-Triggered Processes

**Corpus note:** based on public Synapse sample #800, not real AIMO configuration.

## 1. Integration Overview

Model a multi-resource REST API as Frends processes, preserving both resources' distinct
interaction semantics (sync request/response vs. fire-and-forget). Demonstrates RFI §5 API
Management directly.

## 2. Source and Target Systems

Source: REST clients. Target: `SimpleStockQuoteService` (both resources).

## 3. Trigger

Two separate Frends **HTTP Triggers** — see §5 for why this isn't one trigger.

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture

**Design decision: one Frends process per resource**, not one process trying to internally branch
on path+method. Reasoning: `process_add_http_trigger` binds one route template + one HTTP method
per trigger; forcing both resources into a single process would need a broader route plus manual
path/method dispatch logic, which is less maintainable (Frends Development Principle: prefer
maintainability) than two clearly-named processes. Both processes can share
`SUB-StockQuoteClient`/`ENV-StockQuoteServiceUrl` if built.

- **Process 1 (`GET /view/{symbol}`):** HTTP Trigger (GET, route `stockquote/view/{symbol}`) →
  build SOAP payload from `#trigger.data.pathParameters.symbol` → `HTTP.Request` → Return response.
- **Process 2 (`POST /order`):** HTTP Trigger (POST, route `stockquote/order`) → **Intermediate
  Return** (202 Accepted) → `HTTP.Request` Task to the backend continues executing after the
  caller has already received the response → final Return to end the process. Confirmed via
  docs.frends.com that Intermediate Return genuinely detaches the caller from the remaining
  shapes — this is no longer an open question.

## 6. Data Mapping

See `mapping.md`.

## 7. Business Rules

Resource 2's caller must get an immediate acknowledgment regardless of backend outcome — this is a
business rule (from `FORCE_SC_ACCEPTED`/`OUT_ONLY`), not incidental plumbing.

## 8. Error Handling

Resource 1: standard default error handler around the HTTP.Request. Resource 2: since the caller
already received a 202 before the backend call resolves, a backend failure here needs a different
handling strategy (e.g. dead-letter/retry, alerting) since there's no caller left to return an
error to — this is a genuine design question, not a detail to skip.

## 9. Logging and Monitoring

Resource 2 in particular needs strong logging/monitoring since failures are invisible to the
caller by design — this is exactly the kind of "design for operations" principle that matters more
here than in a synchronous integration.

## 10. Security

Not addressed in source.

## 11. Dependencies

`SUB-StockQuoteClient`/`ENV-StockQuoteServiceUrl` (shared candidates, reusable if built for the
6-sample corpus).

## 12. Flow Diagram

See `flow.mmd`.

## 13. Frends Process Breakdown

Process 1: Trigger → build payload → HTTP.Request → Return.
Process 2: Trigger → Return 202 (structured to genuinely detach from the subsequent HTTP.Request —
see Open Questions for how).

## 14. Migration Considerations

**Key architectural difference:** WSO2's single `<api>` construct with multiple `<resource>`
elements becomes multiple Frends processes sharing a route prefix convention (not a single
process). This is a deliberate, documented divergence, not an oversight — see Continuous
Traceability: original models one API surface; Frends models N processes that together present
the same external surface.

## 15. Testing Strategy

- Resource 1: GET with a valid symbol → confirm correct SOAP payload construction and passthrough
  response.
- Resource 2: POST → confirm caller receives 202 immediately, independent of backend response time;
  confirm the backend call still happens (check logs/downstream effect) even though the caller
  already moved on.

## 16. Open Questions

1. **Resolved:** Intermediate Return is the native mechanism — confirmed via docs.frends.com.
   A plain `Return` (not Intermediate) does end the process and would NOT give this semantic; the
   distinction matters and must not be confused during implementation.
2. Are AIMO's real fire-and-forget-style integrations (if any) using webhooks, queues, or a similar
   async trigger already, rather than this exact HTTP-fire-and-forget shape?

## 17. Assumptions

Assumed two Frends processes is preferable to one internally-dispatching process; assumed
`{{#trigger.data.pathParameters.symbol}}` correctly exposes the path parameter for a route like
`stockquote/view/{symbol}` — not yet verified against a live `process_add_http_trigger` call's
`availableReferences`.

## 18. Reusable Components (existing)

Would reuse `SUB-StockQuoteClient`/`ENV-StockQuoteServiceUrl`.

## 19. New Reusable Components (candidates)

None new, though Resource 2's fire-and-forget pattern (once resolved) may itself become a reusable
pattern documented in `architecture/shared-patterns.md` if AIMO has multiple integrations like it.

## 20. Readiness Assessment

**READY WITH ASSUMPTIONS** — the former blocker (fire-and-forget mechanism) is resolved via
Intermediate Return. Both Process 1 and Process 2 are now buildable designs. Not a real AIMO
integration regardless — no build without explicit approval.
