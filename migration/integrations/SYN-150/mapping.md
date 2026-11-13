# SYN-150 — Data Mapping

This sample is a pure passthrough proxy: no field-level transformation occurs. The message body is
forwarded unchanged from caller to backend and back.

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| (entire SOAP envelope) | none — passthrough | (entire SOAP envelope) | Yes | n/a | No fields are inspected or rewritten in this sample; real AIMO equivalents using this pattern likely do transform and would need their own mapping table. |
