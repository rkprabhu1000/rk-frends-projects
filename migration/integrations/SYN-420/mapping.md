# SYN-420 — Data Mapping

No field-level data transformation — this sample is a caching concern, not a mapping concern.

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| (whole message body) | DOM-hash digest, used as cache key | Cache key | Yes | n/a | Real AIMO usage likely wants a narrower key (e.g. a specific ID field) — see source-analysis.md open question. |
| (backend response) | stored verbatim on cache miss | Cached value | Yes | n/a | Passthrough, no shape change. |
