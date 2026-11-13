# SYN-001 — Source Analysis: Content-Based Routing

**Corpus note:** Apache Synapse public sample #1. Not AIMO Park's own configuration.

## Integration Purpose

Route inbound messages based on inspecting a message property, forwarding only messages that match
a pattern and dropping everything else after forwarding. Demonstrates RFI §3 Orchestration &
Process Flow.

## Existing Architecture

- **Source:** inbound message with a `To` header/property.
- **Target:** `SimpleStockQuoteService` (only for messages that match).
- **Trigger:** any inbound message to the `main` sequence.

## Existing Flow

1. Message arrives at the `main` sequence.
2. `<filter>` evaluates `get-property('To')` against regex `.*/StockQuote.*`.
3. If it matches: send to the StockQuote endpoint, then `<drop/>` (stop further processing —
   response handling isn't shown, this sample is fire-and-forget-shaped).
4. If it doesn't match: falls through to the sequence-level `<send/>` (default passthrough).

## Existing Logic

- Single conditional branch based on regex match against a routed property (`To`), not the message
  body.
- No transformation.

## Existing Configuration

Backend URL hardcoded: `http://localhost:9000/services/SimpleStockQuoteService`.

## Existing Dependencies

Depends on the `To` property being set/meaningful on the inbound message — in HTTP terms this is
typically the destination URI/service name, so this pattern is really "route by which service was
addressed," which is a common front-door dispatch pattern.

## Source Assets

Source behaviour identified from `sample1_content_based_routing.xml` only.

## Findings

- **Confirmed:** single-branch content-based routing via regex on a routed property, with an
  explicit drop-after-send (no synchronous response flow modeled here).
- **Assumption:** in real AIMO usage, "route by destination" dispatch like this is plausible at a
  gateway/front-door layer (e.g. one WSO2 entry point fanning out to many backend integrations) —
  worth asking AIMO directly whether any of their 126 WSO2 integrations use a similar shared
  front-door dispatch, since that would be an architecturally significant pattern to replicate
  faithfully rather than reinvent per-integration.
- **Potential technical debt:** matching on `To` via regex is fragile if the pattern needs to change
  frequently; Frends' equivalent should keep the match expression readable and externally visible
  rather than buried in an inline expression (Development Principles: keep expressions readable).
