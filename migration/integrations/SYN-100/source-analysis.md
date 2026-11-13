# SYN-100 — Source Analysis: WS-Security Policy Binding

**Corpus note:** Apache Synapse public sample #100 (complex tier). Not AIMO Park's own
configuration.

## Integration Purpose

Secure the outbound call to a backend using WS-Security, with the security policy defined entirely
outside the mediation flow (as an externally-loaded policy document), not inline. Demonstrates RFI
§1 Connectivity & Protocol Support and §7 Error Handling & Resilience (declarative, externally
governed security).

## Existing Architecture

- **Source:** inbound message to `main`.
- **Target:** `SecureStockQuoteService` at `http://localhost:9000/services/SecureStockQuoteService`.
- **Security artifact:** `<localEntry key="sec_policy" src="file:repository/conf/sample/resources/policy/policy_3.xml"/>` — a WS-SecurityPolicy XML document loaded by reference, not inlined.

## Existing Flow

1. Message arrives at `main`.
2. `<send>` targets the `secure` endpoint, whose `<address>` carries `<enableSec policy="sec_policy"/>` — binding the externally-loaded policy to this specific endpoint.
3. `<out>`: plain passthrough `<send/>`.

## Existing Logic

None beyond the security binding — no branching, no transformation. The "logic" here is entirely
declarative security configuration attached to an endpoint definition.

## Existing Configuration

- Policy document path: `file:repository/conf/sample/resources/policy/policy_3.xml` (Synapse
  runtime-relative path — not portable as-is).
- Backend URL: `http://localhost:9000/services/SecureStockQuoteService`.

## Existing Dependencies

Depends on the policy XML file being present and valid at deploy time; depends on the backend
actually supporting/requiring the WS-Security handshake described in that policy (unknown from
this sample alone — the policy document itself was not provided in this corpus).

## Source Assets

Source behaviour identified from `sample100_ws_security.xml` only. The referenced `policy_3.xml`
content itself was not included in this corpus — its actual security requirements (WS-Security
1.1 vs 1.0, signing vs. encryption vs. both, timestamp requirements, key material) are unknown.

## Findings

- **Confirmed:** WS-Security is bound declaratively at the endpoint level via a policy reference,
  not implemented as inline mediation logic.
- **Critical unknown:** the actual WS-Security policy content (what it requires — signing,
  encryption, UsernameToken, X.509, timestamps) is not visible in this sample. This determines which
  of the two Frends capability paths below applies — see Findings below and migration-plan.md §5/§20.
- **Verified capability (via docs.frends.com, not memory):** Frends has a dedicated
  `Frends.Soap.Request` Task (v1.1.0, requires Frends 5.7+, cross-platform Agent) with a native
  `IncludeWsSecurity` option. It emits a WS-Security header implementing the **UsernameToken +
  Timestamp** profile (`WsSecurityUsername`, `WsSecurityPassword`, `WsSecurityPasswordType`,
  `WsSecurityTimestampMinutes`) — a genuinely native feature, not a workaround. It also has header
  emission for WS-Addressing, WS-ReliableMessaging, WS-Trust, WS-Federation, and a WS-Policy
  `PolicyReference` (`WsPolicyReferenceUri`).
- **Important nuance — do not overstate this:** `IncludeWsPolicy`/`WsPolicyReferenceUri` only emits
  a *reference* to a policy URI in the header (announcing "this message claims to follow policy X");
  it does **not** parse the referenced WS-SecurityPolicy document and enforce its actual assertions
  (e.g. X.509 signing of specific elements, body encryption). If AIMO's real `policy_3.xml`-equivalent
  requires signing/encryption beyond UsernameToken+Timestamp, `Frends.Soap.Request` does not natively
  implement that — the docs point to a separate, generic `SignXml` Task (XML digital signature) as
  the building block, which would need custom engineering to match a specific WS-SecurityPolicy's
  exact requirements (canonicalization, token references, transforms), with no guarantee of
  conformance out of the box.
- **Net position:** this is genuinely two different answers depending on what the real policy
  requires — see migration-plan.md §20 for the two-branch readiness assessment.
