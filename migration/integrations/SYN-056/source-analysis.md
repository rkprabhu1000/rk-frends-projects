# SYN-056 — Source Analysis: WSDL-Resolved Endpoint

**Corpus note:** Apache Synapse public sample #56. Not AIMO Park's own configuration.

## Integration Purpose

Resolve the actual call target from a WSDL's service+port definition instead of a hardcoded URL.
Demonstrates RFI §1 Connectivity & Protocol Support.

## Existing Architecture

- **Source:** inbound message.
- **Target:** `SimpleStockQuoteService`, resolved via
  `<wsdl uri="file:repository/conf/sample/resources/proxy/sample_proxy_1.wsdl" service="SimpleStockQuoteService" port="SimpleStockQuoteServiceHttpSoap11Endpoint"/>`
  rather than a literal `<address uri="...">`.

## Existing Flow

1. Message arrives at `main`'s `<in>` block.
2. `<send>` targets an endpoint whose address is resolved by reading the referenced WSDL file and
   extracting the binding address for the named service+port.
3. `<out>` block is a plain passthrough `<send/>`.

## Existing Logic

None beyond endpoint resolution — no branching, no transformation.

## Existing Configuration

- WSDL file path: `file:repository/conf/sample/resources/proxy/sample_proxy_1.wsdl` (same WSDL
  file the SYN-150 proxy sample publishes — these two samples are linked in the real corpus,
  confirming the "same backend, different pattern" framing from the README).
- Service name: `SimpleStockQuoteService`. Port: `SimpleStockQuoteServiceHttpSoap11Endpoint`.

## Existing Dependencies

Depends on the WSDL file being present at a Synapse-runtime-relative path, and on that WSDL
correctly declaring the named port's binding address.

## Source Assets

Source behaviour identified from `sample56_wsdl_endpoint.xml` only (and its reference to the same
WSDL file used by `sample150_proxy_service.xml`).

## Findings

- **Confirmed:** this is a *static* WSDL resolution — the file and its contents are fixed at
  deploy/config time, not looked up dynamically at runtime against a live service registry (e.g.
  UDDI). It's "read the address out of this WSDL file once," not "resolve dynamically per-call."
- **Open question (carried into migration plan):** if AIMO's real integrations use WSDL resolution
  for genuinely dynamic service discovery rather than a static file read, the proposed Frends
  design (resolve once, externalize as Environment Variable) would not be sufficient — flag this
  explicitly rather than assume static resolution is universal.
