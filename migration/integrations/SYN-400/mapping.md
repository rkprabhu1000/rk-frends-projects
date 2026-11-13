# SYN-400 — Data Mapping

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| `//m0:getQuote/m0:request` (repeating XPath) | split into N sub-messages | N individual backend requests | Yes | n/a | Frends equivalent: `Foreach` over the parsed list of `request` elements. |
| N backend responses (`//m0:getQuoteResponse`) | recombined on completion | 1 aggregated response | Yes | n/a | Frends equivalent: collect each Foreach iteration's result into a list, assembled into the final response after the loop. |
