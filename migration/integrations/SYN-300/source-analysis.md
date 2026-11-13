# SYN-300 — Source Analysis: Scheduled Polling Task

**Corpus note:** Apache Synapse public sample #300. Not AIMO Park's own configuration.

## Integration Purpose

Fire a message on a fixed timer rather than in response to an inbound call — the only sample in
this corpus that is time-triggered, not request-triggered. Demonstrates RFI §4 Messaging & Event
Handling and §3 Orchestration.

## Existing Architecture

- **Source:** internal timer (`<trigger interval="5"/>` — every 5 seconds).
- **Target:** `SimpleStockQuoteService`, via a `MessageInjector` task that constructs and sends a
  static `getQuote` SOAP request for symbol `IBM`.

## Existing Flow

1. Every 5 seconds, the `CheckPrice` task fires, injecting a static SOAP message
   (`m0:getQuote`/`m0:symbol=IBM`) addressed to the backend, with `soapAction=urn:getQuote`.
2. The injected message flows through `main`'s `<in>` block (`<send/>` — passthrough to backend).
3. The response flows through `<out>`, where a custom log statement extracts three XPath values
   from the SOAP response (`lastTradeTimestamp`, `name`, `last`) and logs them.

## Existing Logic

No conditionals — a fixed-interval poll with a static payload, plus response field extraction for
logging (not further routing).

## Existing Configuration

- Poll interval: 5 seconds (`<trigger interval="5"/>`).
- Target URL, SOAP action, message format: hardcoded in the task's properties.
- Static payload: hardcoded symbol `IBM`.

## Existing Dependencies

Depends on the backend being reachable on every poll cycle; no dependency on other
sequences/endpoints in this sample (self-contained, unlike SYN-003/SYN-004).

## Source Assets

Source behaviour identified from `sample300_scheduled_task.xml` only.

## Findings

- **Confirmed:** simple fixed-interval scheduled poll with a static request payload and
  response-field logging.
- **Potential migration challenge:** a 5-second interval is extremely aggressive for a real
  integration; this is a demo-scale value, not representative of a real polling cadence. Flag
  explicitly rather than carry it forward literally if this pattern is ever applied to a real AIMO
  integration.
- **Assumption:** the static `IBM` payload stands in for whatever real, possibly parameterized,
  payload a real AIMO scheduled integration would use (e.g., "poll NetSuite for changed orders")
  — this sample doesn't exercise payload parameterization at all.
