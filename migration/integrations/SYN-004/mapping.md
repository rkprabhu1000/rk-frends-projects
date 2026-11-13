# SYN-004 — Data Mapping

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| `//m0:getQuote/m0:request/m0:symbol` (SOAP body XPath) | exact match: IBM / MSFT / SUN | Routing decision (branch selector) | Yes | n/a | Drives which of 3 branches executes; unmatched symbols have no defined branch in this sample (open question). |
| `ERROR_MESSAGE` (Synapse error context property) | direct passthrough via `get-property('ERROR_MESSAGE')` | Log field | No | n/a | Consumed identically by both the default `fault` sequence and the `sunErrorHandler` override. |
