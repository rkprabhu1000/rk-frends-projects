# SYN-100 — Data Mapping

No field-level data transformation — pure passthrough once the (unknown) WS-Security handling is
applied.

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| (entire SOAP envelope) | WS-Security signing/encryption per external policy (content unknown) | (entire SOAP envelope, security-wrapped) | Yes | n/a | Cannot be mapped with confidence — the actual policy document was not available in this corpus. |
