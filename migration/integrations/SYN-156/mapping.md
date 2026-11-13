# SYN-156 — Data Mapping

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| (original request body) | saved once, replayed to both backends | `#var.OriginalRequest` (Frends process variable) | Yes | n/a | Replaces the source's `enrich source="body" target="property:REQUEST"` — no need to restore-to-body in Frends since both backend calls can read the same variable directly. |
| `//ns:getQuoteResponse/ns:return/ns:last` (secure backend response) | XPath extraction | `#var.SecureAmount` | Yes | n/a | Replaces `SECURE_SER_AMT` property + `enrich...SecureService_Res`. |
| `//ns:getQuoteResponse/ns:return/ns:last` (simple backend response) | XPath extraction | `#var.SimpleAmount` | Yes | n/a | Replaces `SIMPLE_SER_AMT` property + `enrich...SimpleService_Res`. |
| `#var.SimpleAmount` vs `#var.SecureAmount` | numeric comparison | Final response selection | Yes | n/a | Return whichever backend's full response corresponds to the higher amount. |
