# SYN-700 — Data Mapping

No field-level transformation — this sample is purely a durability/timing concern.

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| (entire inbound message) | persisted verbatim | Queue message body | Yes | n/a | Frends equivalent: message lands on a queue as-is. |
| (queued message) | forwarded verbatim on poll | Backend request body | Yes | n/a | Passthrough — no transformation, just delayed delivery. |
