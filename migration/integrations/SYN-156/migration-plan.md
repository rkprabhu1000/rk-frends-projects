# SYN-156 — Migration Plan: Enrich/Multi-Sequence Comparison → Sequential Calls + Variable Comparison

**Corpus note:** based on public Synapse sample #156, not real AIMO configuration. Depends on
SYN-100's WS-Security findings.

## 1. Integration Overview

Call two backends with the same request, compare a numeric field from each, return the better
result. Demonstrates RFI §3/§11. The Frends design deliberately does **not** replicate the
source's enrich-based save/restore mechanics — see §14.

## 2. Source and Target Systems

Source: inbound message. Targets: `SecureStockQuoteService` (WS-Security) and
`SimpleStockQuoteService` (unprotected), both called with the same original request.

## 3. Trigger

Frends HTTP Trigger (assumed).

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture

1. Trigger receives the request; save it as a process variable (no need to save-to-property and
   restore-to-body the way Synapse does — Frends variables persist independently of the "current
   message").
2. Call `SecureStockQuoteService` (via `Frends.Soap.Request`, per SYN-100's Branch A/B — **this
   step is blocked on the same open question as SYN-100**). Extract and store the amount.
3. Call `SimpleStockQuoteService` (via `HTTP.Request`, same request body from step 1) — no
   security. Extract and store the amount.
4. `ExclusiveGateway`: compare the two stored amounts.
5. Return whichever full response corresponds to the higher amount.

## 6. Data Mapping

See `mapping.md`.

## 7. Business Rules

Return the response with the higher `last` value between the two backends — a genuine business
comparison rule, preserved exactly (just re-expressed without enrich mechanics).

## 8. Error Handling

Recommend the standard default error handler for both backend calls; additionally, decide
explicitly what happens if one backend succeeds and the other fails (source doesn't address this
at all — no error handling visible in the sample). Recommend: if either call fails, return an
error rather than silently picking the one that succeeded, since "the better of two responses"
implicitly assumes both were obtained.

## 9. Logging and Monitoring

Preserve both amount-logging statements from the source; additionally log which backend "won" the
comparison — the source technically has this via the `<log>` statements inside `<then>`/`<else>`,
so this is direct parity, not an addition.

## 10. Security

Same open item as SYN-100 for the secure backend call. The simple backend call has no security.

## 11. Dependencies

**Directly depends on SYN-100's resolution** (which WS-Security branch applies) before this plan
can be finalized for the secure call. Also depends on `SUB-StockQuoteClient`/
`ENV-StockQuoteServiceUrl` conventions for the simple call if reused.

## 12. Flow Diagram

See `flow.mmd`.

## 13. Frends Process Breakdown

Trigger → save request variable → Frends.Soap.Request (secure) → extract/store amount →
HTTP.Request (simple) → extract/store amount → ExclusiveGateway (compare) → Return winning
response.

## 14. Migration Considerations

**Deliberate simplification, not a faithful port:** the source's 5 `<enrich>` operations exist
solely to work around Synapse's single-current-message execution model (only one message/body is
"active" at a time, so anything that needs to survive past the next call must be saved to a
property first). Frends process variables don't have this constraint — a Frends process can hold
`OriginalRequest`, `SecureAmount`, `SimpleAmount`, `SecureResponse`, and `SimpleResponse`
simultaneously as ordinary named variables. Replicating the enrich save/restore choreography
literally would be needlessly complex and a worse Frends design. Documented per Continuous
Traceability: what the original did (5 enrich ops to manage single-message-slot constraints), what
Frends does (parallel named variables), why it differs (Frends has no such constraint), why it's
safe (identical externally observable comparison logic and final response).

**Also note:** the source's 3-way split across `StockQuoteProxy`/`SimpleServiceSeq`/`ClientOutSeq`
(connected only by `receive="..."` attributes) collapses into **one linear Frends process** — the
continuation-via-attribute-reference mechanism has no Frends equivalent need, since a single
process can just keep going after each call.

## 15. Testing Strategy

- Both backends return valid responses, simple > secure → confirm simple response returned.
- Both backends return valid responses, secure ≥ simple → confirm secure response returned.
- One backend fails → confirm the explicit failure behaviour (§8) fires, not a silent
  pick-the-survivor behaviour.

## 16. Open Questions

1. **Inherited from SYN-100, blocking:** which WS-Security branch (UsernameToken+Timestamp vs.
   policy-driven signing/encryption) does the secure call actually need? Resolve SYN-100 first.
2. What should happen if one backend fails? (§8 — decided here as "fail explicitly," pending
   AIMO sign-off if this becomes a real integration.)

## 17. Assumptions

Assumed Frends process variables are an acceptable, better replacement for the source's enrich
save/restore mechanics — a design improvement rather than a literal port, consistent with the
migration-architect principle of not blindly recreating source-platform workarounds.

## 18. Reusable Components (existing)

Would reuse `SUB-StockQuoteClient`/`ENV-StockQuoteServiceUrl` for the simple-backend call, and
whatever `SUB-SoapSecureClient` component SYN-100 originates for the secure call.

## 19. New Reusable Components (candidates)

None new beyond what SYN-100 already proposes.

## 20. Readiness Assessment

**NOT READY** — blocked on SYN-100's open WS-Security branch question. The non-security parts of
this design (variable-based comparison instead of enrich choreography) are sound and low-risk on
their own. Not a real AIMO integration regardless — no build without explicit approval.
