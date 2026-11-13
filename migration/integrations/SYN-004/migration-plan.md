# SYN-004 — Migration Plan: Error Handling → Process Error Handler + Scoped Catch

**Corpus note:** based on public Synapse sample #4, not real AIMO configuration.

## 1. Integration Overview

Preserve Synapse's two-tier error model (default fault sequence + named per-branch override) using
Frends' own error-handling primitives. Demonstrates RFI §7 Error Handling & Resilience.

## 2. Source and Target Systems

Source: inbound `getQuote` request. Target: real backend (IBM branch); two intentionally-broken
stand-in endpoints (MSFT, SUN branches) used only to exercise error paths.

## 3. Trigger

Frends HTTP Trigger (assumed).

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture

1. Trigger receives the request.
2. `ExclusiveGateway` on the stock symbol (3 branches: IBM / MSFT / SUN).
3. IBM, MSFT branches: `HTTP.Request` Task, protected by the **process-level default error
   handler** (`SUB-DefaultErrorHandler`, per `architecture/reusable-components.md`).
4. SUN branch: Call Activity into its own subprocess, wrapped in a **scoped `Catch`** shape that
   overrides the default handler for this branch only — mirroring Synapse's `onError` attribute.
5. Both paths log the error context, then terminate cleanly.

## 6. Data Mapping

See `mapping.md`.

## 7. Business Rules

Route by exact-match stock symbol; unmatched symbols have no defined behaviour in the source (open
question below).

## 8. Error Handling

- **Default:** `SUB-DefaultErrorHandler` attached at the process level — Frends-native equivalent
  of Synapse's default `fault` sequence, logging the equivalent of `ERROR_MESSAGE`.
- **Override:** a scoped `Catch` around the SUN branch only, equivalent to `onError="sunErrorHandler"`
  — Frends' scoping mechanism achieves the same "override for this one flow" semantics without a
  try/catch-in-Code-element approach (see [[feedback_frends_no_try_catch]] — empty
  catch/try-catch-in-Code causes validation errors; use the scoped shape instead).

## 9. Logging and Monitoring

Preserve the custom log-level message including the error text, for both the default and the
override path.

## 10. Security

Not addressed in source.

## 11. Dependencies

`SUB-DefaultErrorHandler` (candidate, originates here). Also depends on
`SUB-StockQuoteClient`/`ENV-StockQuoteServiceUrl` for the IBM branch's real call.

## 12. Flow Diagram

See `flow.mmd`.

## 13. Frends Process Breakdown

1. Trigger.
2. ExclusiveGateway (symbol).
3. IBM/MSFT branches → HTTP.Request, default error handler active.
4. SUN branch → Call Activity → scoped Catch (override).
5. Both error paths → Log → terminate.

## 14. Migration Considerations

Synapse scopes error overrides *by which sequence failed* (`onError` on `sunSequence`); Frends
scopes them *by which shape/branch is wrapped in Catch*. These are functionally equivalent for this
sample but the underlying mechanism differs — worth calling out explicitly rather than implying
Frends has a literal `onError`-attribute equivalent.

## 15. Testing Strategy

- IBM branch, backend reachable → success path.
- MSFT branch (broken endpoint) → confirm default error handler fires, not the SUN override.
- SUN branch, backend unreachable → confirm the scoped Catch override fires instead of the default.

## 16. Open Questions

1. What should happen for a stock symbol that matches none of the three cases? Undefined in the
   source sample — needs an explicit decision (silent drop vs. explicit "unsupported symbol" error)
   rather than inheriting Synapse's implicit fall-through.
2. The `bogus` and `sunPort` endpoint keys are undefined in this file alone — confirm they're
   intentional demonstration stand-ins, not evidence of a missing sample file.

## 17. Assumptions

Assumed "scope the Catch around the SUN branch" is an acceptable functional equivalent to Synapse's
`onError` attribute; not yet validated against a broader set of real AIMO error-handling patterns.

## 18. Reusable Components (existing)

Would reuse `SUB-StockQuoteClient`/`ENV-StockQuoteServiceUrl` from SYN-003 if built first.

## 19. New Reusable Components (candidates)

`SUB-DefaultErrorHandler` (subprocess/process-level error handler) — added to
`architecture/reusable-components.md`.

## 20. Readiness Assessment

**READY WITH ASSUMPTIONS** for the PoC conversion exercise. Not a real AIMO integration — no build
without explicit approval.
