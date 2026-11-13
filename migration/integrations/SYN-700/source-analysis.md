# SYN-700 — Source Analysis: Message Store + Scheduled Forwarding Processor

**Corpus note:** Apache Synapse public samples #700 + #702, combined into one file since they're
documented as working together. Not AIMO Park's own configuration.

## Integration Purpose

Guaranteed/async delivery: persist every inbound message durably first, then forward it to the
backend on a separate schedule — decoupling receipt from delivery so a backend outage doesn't lose
messages. Demonstrates RFI §4 Messaging & Event Handling and §7 Error Handling & Resilience
(durability, not just retry).

## Existing Architecture

Two **decoupled config artifacts** that only work together as one logical unit:
- **`main` sequence:** on receipt, sets `FORCE_SC_ACCEPTED=true` (immediate ack to the caller,
  same Axis2 mechanism seen in SYN-800's Resource 2) and `<store messageStore="MyStore" sequence="onStoreSequence"/>` — persists the message, running `onStoreSequence` (just a log) as a
  side-effect of storing.
- **`messageStore name="MyStore"`:** the durable store itself (no backend specified in this
  sample — defaults to an in-memory/JMS-agnostic store; a real deployment would back this with
  something durable, e.g. JMS).
- **`messageProcessor class="...ScheduledMessageForwardingProcessor" name="ScheduledProcessor" messageStore="MyStore">`: a *separate* scheduled construct that polls `MyStore` every 10 seconds (`<parameter name="interval">10000</parameter>`) and forwards each stored message onward — **not shown as forwarding to the backend explicitly in this sample; the processor's target is implicit/default in Synapse's own configuration model, not visible here.**

## Existing Flow

1. Inbound message arrives at `main`.
2. Immediate accept signaled to caller (`FORCE_SC_ACCEPTED`) — caller does not wait for actual
   delivery.
3. Message persisted into `MyStore`; `onStoreSequence` logs "Storing message."
4. **Completely separately**, on a 10-second interval, `ScheduledProcessor` polls `MyStore` and
   forwards stored messages — this is a different top-level construct, not a step inside `main`.

## Existing Logic

None beyond store-then-poll-then-forward. No transformation, no branching.

## Existing Configuration

- Poll interval: 10 seconds (demo-scale value, same caveat as SYN-300's 5-second interval).
- Store name: `MyStore`, referenced by both the storing sequence and the processor.

## Existing Dependencies

The `messageProcessor` depends on `MyStore` (by name); the receiving flow's guarantee (no message
lost even if the backend is down) depends on the store actually being durable, which this sample
doesn't specify (no persistence backend configured).

## Source Assets

Source behaviour identified from `sample700_702_messagestore_processor.xml` only (combining
official samples #700 and #702, per the complex-tier README's note that this file splices two
adjacent samples documented as working together).

## Findings

- **Confirmed:** this is a genuine store-and-forward pattern spread across two separate top-level
  Synapse constructs (`main`+`messageStore` vs. `messageProcessor`) — the corpus README's stated
  test intent ("does the AI recognize these as one logical unit") is a real and valid concern; a
  naive per-file conversion could easily treat `messageProcessor` as an unrelated, orphaned
  artifact.
- **Potential migration challenge:** Frends' most direct equivalent is a **Queue-based trigger**
  (message lands on a queue instead of being processed synchronously) paired with a consumer
  process — but this changes the infrastructure requirement (a real message queue/broker is now a
  prerequisite) compared to Synapse's in-process message store, which needs no external broker by
  default. This is a materially different infrastructure footprint, not just a syntax translation.
- **Resolved via docs.frends.com (2026-09-16):** the "ack the caller now" half of this pattern
  (Process 1) is natively supported via **Intermediate Return** — same resolved mechanism as
  SYN-800. This narrows the remaining open question to just the queue/broker choice (Process 2's
  infrastructure), not the ack-then-continue mechanic.
- **Unknown:** what backs `MyStore` in a real deployment (in-memory = not actually durable across
  restarts, vs. JMS-backed = genuinely durable) — this sample doesn't specify, and the distinction
  matters enormously for whether "guaranteed delivery" is actually true.
- **Assumption:** the forwarding target is the same `SimpleStockQuoteService` used throughout the
  corpus, even though this specific sample never states it — inferred from the corpus's stated
  convention (all 6+9 samples share the same fictional backend), not confirmed in this file.
