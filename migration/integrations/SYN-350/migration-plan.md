# SYN-350 — Migration Plan: Script Mediator → Code Element (structure only)

**Corpus note:** based on public Synapse sample #350, not real AIMO configuration.

## 1. Integration Overview

Preserve two custom-logic hook points (request transform, response transform) as Frends Code
elements. Demonstrates RFI §3/§11 — but the actual logic cannot be specified from this sample.

## 2. Source and Target Systems

Source: inbound message. Target: `SimpleStockQuoteService`.

## 3. Trigger

Frends HTTP Trigger (assumed).

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture

1. Trigger receives the request.
2. **Code element** (`UseStatementMode = true`, per Frends process-authoring guidance) — structural
   placeholder for the ported `transformRequest` logic.
3. `HTTP.Request` Task → backend.
4. **Code element** — structural placeholder for the ported `transformResponse` logic.
5. Return.

## 6. Data Mapping

See `mapping.md` — cannot be completed without the real script content.

## 7. Business Rules

Unknown — embedded in the scripts, not visible in the XML wiring.

## 8. Error Handling

Not modeled in source. Recommend the standard default error handler; additionally, any exception
thrown inside a Code element should be handled explicitly (per [[feedback_frends_no_try_catch]] —
prefer `TryParse`-style guards over try/catch in Code elements where possible).

## 9. Logging and Monitoring

Not present in source; recommend standard logging once real logic is known.

## 10. Security

Unknown — cannot assess without the real script content (e.g. does it handle any sensitive
fields?).

## 11. Dependencies

None beyond the shared backend call pattern.

## 12. Flow Diagram

See `flow.mmd`.

## 13. Frends Process Breakdown

Trigger → Code (request transform placeholder) → HTTP.Request → Code (response transform
placeholder) → Return.

## 14. Migration Considerations

**This plan can only specify structure, not behaviour.** A real migration of this pattern requires
the actual `.js` file contents; porting JavaScript idioms to C# (Frends' Code element language) is
usually mechanical for simple transforms but needs review for anything using JS-specific
libraries/APIs not available in .NET.

## 15. Testing Strategy

Cannot be defined until the real script logic is known — testing strategy would need to compare
old-script-output vs. new-Code-element-output on the same sample inputs.

## 16. Open Questions

1. **Blocking:** what do `transformRequest.js` and `transformResponse.js` actually do? Needed
   before this plan can move past structural placeholder status.
2. Why do the two scripts use different reference mechanisms (`localEntry`+key vs. direct registry
   path) for what is conceptually the same kind of artifact? Worth checking whether this reflects
   an intentional distinction or just historical inconsistency, in case AIMO's real estate has the
   same pattern at scale.

## 17. Assumptions

Assumed a straightforward JS-to-C# port is feasible; not verified without the real script content.

## 18. Reusable Components (existing)

None applicable yet.

## 19. New Reusable Components (candidates)

None — premature without knowing the real transformation logic.

## 20. Readiness Assessment

**NOT READY.** Blocked on missing source material (the actual script files), not on any Frends
capability gap. Do not build a "placeholder" Code element in Frends and call it done — that would
misrepresent unported business logic as migrated. Not a real AIMO integration regardless.
