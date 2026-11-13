# SYN-055 — Data Mapping

No field-level transformation — this sample is purely endpoint-topology/resilience configuration.

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| Client session identifier (mechanism unknown) | used to pin subsequent calls to the same failover group | Endpoint selection | Yes | n/a | Cannot be fully specified — the real session-key mechanism isn't visible in this sample. |
| (message body) | passthrough | (message body) | Yes | n/a | No transformation, just routed to whichever endpoint the topology selects. |
