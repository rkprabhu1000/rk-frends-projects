# SYN-800 — Source Analysis: REST API, Multiple Resources

**Corpus note:** Apache Synapse public sample #800 (complex tier). Not AIMO Park's own
configuration.

## Integration Purpose

Expose a genuine REST API (path + method routed, not a single proxy) with two resources that
behave differently — one a synchronous GET with path-parameter binding, one an async-style POST —
bridging REST to a backend SOAP call. Demonstrates RFI §5 API Management directly (API-shape
modeling: paths, methods, params — not just message mediation).

## Existing Architecture

- **Construct:** `<api name="StockQuoteAPI" context="/stockquote">` — a first-class REST API
  definition, distinct from both the `<proxy>` (SYN-150) and plain `<sequence>` (SYN-001/003/004)
  constructs seen elsewhere in this corpus.
- **Resource 1:** `uri-template="/view/{symbol}"`, `methods="GET"` — path-parameter binding.
- **Resource 2:** `url-pattern="/order/*"`, `methods="POST"` — wildcard path matching, a
  differently-typed pattern than Resource 1's URI template.
- **Target:** `SimpleStockQuoteService` for both resources.

## Existing Flow

**Resource 1 (`GET /view/{symbol}`):**
1. `<payloadFactory>` builds a `getQuote` SOAP request, injecting the path parameter
   (`get-property('uri.var.symbol')`) as the `symbol` value — REST-to-SOAP bridging via templated
   payload construction.
2. `<send>` to the backend (`format="soap11"` explicit on the endpoint).
3. `<outSequence>`: plain passthrough `<send/>`.

**Resource 2 (`POST /order/*`):**
1. Sets `FORCE_SC_ACCEPTED=true` (Axis2-scope property) and `OUT_ONLY=true` — signals this is a
   fire-and-forget call; the caller gets an immediate 202-style acknowledgment rather than waiting
   for the backend's actual response.
2. `<send>` to the same backend.
3. **No `<outSequence>` at all** — confirming the fire-and-forget intent; there is nothing to do
   with a response because none is awaited back to the caller.

## Existing Logic

- Resource 1: templated payload construction from a path parameter — a real transformation, not
  passthrough.
- Resource 2: deliberately asynchronous/fire-and-forget semantics via Axis2-specific properties.
- Both resources hit the same backend but with fundamentally different interaction semantics
  (request/response vs. fire-and-forget).

## Existing Configuration

Backend URL: `http://localhost:9000/services/SimpleStockQuoteService` (both resources); explicit
`format="soap11"` on both.

## Existing Dependencies

None beyond the shared backend.

## Source Assets

Source behaviour identified from `sample800_rest_api_multi_resource.xml` only.

## Findings

- **Confirmed:** two resources under one API context, with genuinely different HTTP-binding styles
  (URI template vs. URL pattern) and genuinely different interaction semantics (sync vs.
  fire-and-forget).
- **Potential migration challenge:** Frends' HTTP Trigger (`process_add_http_trigger`) binds one
  route template to one process; representing "one API with multiple resources" the way WSO2's
  `<api>` construct does means either (a) one Frends process per resource, each with its own route,
  or (b) a single process with a broader route plus internal routing by path/method — a genuine
  design decision, not a mechanical translation. See migration-plan.md §5.
- **Resolved via docs.frends.com (2026-09-16):** Frends has a native **Intermediate Return** shape
  (`process_add_intermediate_return`) that sends the HTTP response and closes the connection
  *without* ending the process — execution continues to the eventual final Return. This is exactly
  the fire-and-forget semantic Resource 2 needs; it's a genuine native feature, not a workaround.
  One caveat from the docs: Intermediate Return is intended for use only once per process instance
  (additional ones are disregarded) — fine for this single-use case.
- **Assumption:** `payloadFactory`-style templated SOAP construction from a path parameter maps
  cleanly onto a Frends `text`/`xml`-mode parameter with `{{...}}` interpolation — low risk.
