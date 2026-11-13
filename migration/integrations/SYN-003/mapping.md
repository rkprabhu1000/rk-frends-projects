# SYN-003 — Data Mapping

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| `version` (localEntry, key="version") | direct passthrough via `get-property('version')` | Environment Variable `ENV-Version` | No | `0.1` | Static config value, not runtime data — becomes an environment variable, not a mapped field. |
| `direction` (property set to `incoming`) | literal assignment | Log field | No | `incoming` | Set once in `main`, consumed in `stockquote`'s log statement. |
| (message body) | none — passthrough | (message body) | Yes | n/a | Forwarded unchanged to the `simple` endpoint. |
