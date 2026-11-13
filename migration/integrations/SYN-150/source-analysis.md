# SYN-150 — Source Analysis: Proxy Service

**Corpus note:** Apache Synapse public sample #150. Not AIMO Park's own configuration — see
migration-dashboard.md scope framing.

## Integration Purpose

Front a backend SOAP service with a named proxy that publishes its own WSDL, so a caller sees a
single stable service contract rather than calling the backend directly. This is the closest
Synapse analogue to "expose an integration as a callable API," which maps to RFI §5 API Management.

## Existing Architecture

- **Source:** any SOAP client calling the proxy's published address.
- **Target:** `SimpleStockQuoteService` at `http://localhost:9000/services/SimpleStockQuoteService`.
- **Trigger:** inbound SOAP request to the proxy endpoint.
- **Middleware:** WSO2/Synapse `<proxy>` construct.

## Existing Flow

1. Client sends a SOAP request to the `StockQuoteProxy` proxy service.
2. Synapse forwards the message to the target endpoint (`SimpleStockQuoteService`).
3. The response passes through the proxy's `outSequence`, which contains a single `<send/>` —
   i.e., passthrough with no transformation.
4. The proxy publishes a static WSDL (`sample_proxy_1.wsdl`) as its own contract, decoupling the
   caller-facing contract from the backend's actual WSDL.

## Existing Logic

- No transformation, no business rules, no conditional logic, no error handling defined in this
  sample (relies on Synapse's process-wide default `fault` sequence, not shown here — see SYN-004).
- Pure passthrough proxy.

## Existing Configuration

- Backend URL: `http://localhost:9000/services/SimpleStockQuoteService` (hardcoded in the sample).
- Published WSDL path: `file:repository/conf/sample/resources/proxy/sample_proxy_1.wsdl` (local file
  reference — a WSO2/Synapse-runtime-specific path convention, not portable as-is).

## Existing Dependencies

- Depends on the target backend being reachable at the hardcoded URL.
- Depends on a WSDL file being present at a Synapse-runtime-relative path.

## Source Assets

Source behaviour identified from `sample150_proxy_service.xml` only.

## Findings

- **Confirmed:** the proxy is a pure passthrough — no mediation logic beyond routing and WSDL
  publication.
- **Assumption:** in a real AIMO integration using this pattern, the "outSequence" would likely
  contain actual transformation/mapping logic, not just `<send/>` — this sample under-represents
  that. Flag as a question once real assets are available.
- **Unknown:** how the published WSDL is actually served/versioned in AIMO's real WSO2 setup (proxy
  WSDL publishing conventions vary by WSO2 product — ESB vs. EI vs. MI).
- **Potential migration challenge:** Frends does not have a direct "publish this API's own WSDL"
  primitive in the same sense as a Synapse proxy; if AIMO's real integrations rely on
  contract-first WSDL publishing for consumers, that needs explicit discussion (see migration-plan
  Open Questions).
