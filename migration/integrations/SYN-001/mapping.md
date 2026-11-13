# SYN-001 — Data Mapping

No field-level data transformation occurs — this sample is pure routing logic, not mapping.

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| `To` (message property / destination) | Regex match `.*/StockQuote.*` | Routing decision (branch selector) | Yes | n/a | Drives which branch executes; not a data mapping in the traditional sense. |
| (message body) | none — passthrough on matched branch | (message body) | Yes | n/a | Forwarded unchanged to the backend. |
