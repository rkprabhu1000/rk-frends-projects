# SYN-800 — Data Mapping

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| `uri.var.symbol` (path parameter, Resource 1) | templated into SOAP payload via `payloadFactory` | `getQuote/request/symbol` (SOAP body) | Yes | n/a | Frends equivalent: `{{#trigger.data.pathParameters.symbol}}` interpolated into a text/xml-mode SOAP envelope template. |
| (POST body, Resource 2) | passthrough, fire-and-forget | (forwarded body) | Yes | n/a | No response mapped back — caller gets an immediate ack, not the backend's actual response. |
