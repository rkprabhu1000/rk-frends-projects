# SYN-100 — Migration Plan: WS-Security Policy Binding → `Frends.Soap.Request` (conditional)

**Corpus note:** based on public Synapse sample #100, not real AIMO configuration. Capability
claims below verified against docs.frends.com on 2026-09-16 — not from memory.

## 1. Integration Overview

Secure the backend call per an externally-defined WS-Security policy. Demonstrates RFI §1/§7. The
Frends-native answer depends entirely on what the real policy requires — this plan gives both
branches rather than guessing.

## 2. Source and Target Systems

Source: inbound message. Target: `SecureStockQuoteService`, WS-Security-protected.

## 3. Trigger

Frends HTTP Trigger (assumed).

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture — two branches

**Branch A — if the real policy is UsernameToken + Timestamp only** (a common, lightweight
WS-Security profile): use the native **`Frends.Soap.Request`** Task directly.
- `IncludeWsSecurity = true`, `WsSecurityUsername`/`WsSecurityPassword` (password from an
  Environment Variable of type `secret`, never a literal), `WsSecurityPasswordType` set per the
  policy's required token type, `WsSecurityTimestampMinutes` set to the policy's validity window.
- This is a **native feature**, not a workaround — Frends 5.7+, cross-platform Agent (AIMO's
  Development/Default01 Agent Group is cross-platform per `get_overview`, so no infrastructure
  blocker here).

**Branch B — if the real policy requires X.509 signing/encryption of specific SOAP elements**:
`Frends.Soap.Request` does not natively enforce arbitrary WS-SecurityPolicy assertions. The
building block would be the generic `SignXml` Task (XML digital signature) combined with manual
SOAP envelope construction — a genuinely custom implementation, not a supported native path.
This branch should not be attempted without an actual policy document and a design review with the
Frends product/R&D team, since XML-signature conformance to a specific WS-SecurityPolicy is
notoriously easy to get subtly wrong (canonicalization, token reference style, transform order).

## 6. Data Mapping

See `mapping.md` — incomplete pending the real policy document (needed to pick Branch A vs. B).

## 7. Business Rules

Unknown — depends on the actual security policy content.

## 8. Error Handling

`Frends.Soap.Request` has `ThrowErrorOnFailure`/`ErrorMessageOnFailure` options — standard error
surface, same pattern as `HTTP.Request`. Recommend the standard default error handler regardless
of branch.

## 9. Logging and Monitoring

Recommend NOT logging full request/response bodies for this integration, since a WS-Security
payload may carry sensitive fields (credentials in UsernameToken, signed/encrypted content).

## 10. Security

- **Branch A:** `WsSecurityPassword` must be sourced from a Frends `secret`-type Environment
  Variable, never a literal — per Frends Development Principles (never hardcode credentials).
- **Branch B:** any key material for signing must also be externalized/secured; not evaluable
  further without the real policy.

## 11. Dependencies

`Frends.Soap.Request` Task (needs to be imported into the tenant via `import_task` if not already
present — not yet checked/imported in this tenant). Branch B additionally depends on `SignXml`.

## 12. Flow Diagram

See `flow.mmd` — updated to show the resolved native task instead of a question mark, with the
branch decision noted inline.

## 13. Frends Process Breakdown

Trigger → `Frends.Soap.Request` Task (Branch A config) → Return. (Branch B would insert a
signing/encryption step before the SOAP call — not detailed further without the real policy.)

## 14. Migration Considerations

Initial analysis (before checking docs.frends.com) assumed Frends had no WS-Security equivalent at
all — that was wrong. The corrected position: **UsernameToken + Timestamp is natively supported**;
full policy-driven signing/encryption is not, and would be a bespoke build. This distinction matters
for the RFI response — "WS-Security" is not one capability, and the honest answer depends on which
profile AIMO's real integrations actually use.

## 15. Testing Strategy

- Branch A: confirm the emitted SOAP envelope carries a valid WS-Security UsernameToken +
  Timestamp header matching what the backend expects; confirm a request with an expired timestamp
  or wrong credentials is rejected as expected.
- Branch B: not defined until a real policy and design are available.

## 16. Open Questions

1. **Resolved (was blocking):** does Frends support WS-Security? Yes, natively for
   UsernameToken+Timestamp via `Frends.Soap.Request`; not natively for full policy-driven
   signing/encryption.
2. **Still open:** what does AIMO's real WS-Security usage (if any) actually require? This
   determines Branch A vs. B and cannot be answered from this public sample alone — the referenced
   `policy_3.xml` was never provided in this corpus.
3. Is `Frends.Soap.Request` currently imported into the `rktestfrends` tenant? Not yet checked —
   verify with `list_tasks` before any build.

## 17. Assumptions

Assumed Frends 5.7+/cross-platform Agent requirements are already satisfied by the tenant (per
`get_overview`, Development/Default01 is cross-platform) — not independently re-verified against
the tenant's exact platform version.

## 18. Reusable Components (existing)

None applicable yet.

## 19. New Reusable Components (candidates)

- **`SUB-SoapSecureClient`** (subprocess, candidate) — a reusable wrapper around
  `Frends.Soap.Request` with WS-Security UsernameToken pre-wired to an Environment Variable
  pattern, for any real AIMO integration that turns out to need Branch A. Not built.

## 20. Readiness Assessment

**READY WITH ASSUMPTIONS for Branch A** (UsernameToken + Timestamp) — this is now a well-understood,
native Frends capability. **NOT READY for Branch B** (policy-driven signing/encryption) — genuinely
blocked on the real policy document and a dedicated design review. Not a real AIMO integration
regardless — no build without explicit approval, and confirm which branch applies before any build
is even attempted.
