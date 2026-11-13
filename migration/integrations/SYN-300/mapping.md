# SYN-300 — Data Mapping

| Source Field | Transformation | Target Field | Mandatory | Default | Notes |
|---|---|---|---|---|---|
| (static) symbol `IBM` | literal, hardcoded | SOAP request body (`getQuote`/`symbol`) | Yes | `IBM` | No parameterization in this sample — a real integration would likely need this dynamic. |
| Response `//ns:return/lastTradeTimestamp` | XPath extraction | Log field `Stock_Quote_on` | No | n/a | Logged only, not routed further. |
| Response `//ns:return/name` | XPath extraction | Log field `For_the_organization` | No | n/a | |
| Response `//ns:return/last` | XPath extraction | Log field `Last_Value` | No | n/a | |
