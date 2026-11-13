# SYN-056 — Data Mapping

No field-level transformation — purely an endpoint-resolution concern.

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| WSDL `service`+`port` binding address | resolved once from WSDL file at build/config time | `ENV-StockQuoteServiceUrl` | Yes | n/a | Static resolution, not dynamic per-call lookup — see source-analysis.md open question. |
| (message body) | none — passthrough | (message body) | Yes | n/a | |
