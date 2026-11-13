# SYN-360 — Data Mapping

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| `//m0:getQuote/m0:request/m0:symbol` (SOAP body XPath) | bound as SQL parameter | `company.name` (DB query parameter) | Yes | n/a | Parameterized — preserve this, never string-concatenate. |
| `company.id` (DB column) | direct read | Routing decision (found vs. not found) | Yes | n/a | Source treats `c1`/`c2`/`c3` as 3 separate cases; recommended Frends design collapses to one "found" branch (see source-analysis.md simplification opportunity) — specific ID used only for logging, not branching logic. |
