# SYN-700 — Migration Plan: Message Store + Processor → Queue Trigger + Consumer Process

**Corpus note:** based on public Synapse samples #700+#702, not real AIMO configuration.

## 1. Integration Overview

Decouple message receipt from delivery using durable storage, so backend outages don't lose
messages. Demonstrates RFI §4/§7. This is a two-process design in Frends, mirroring the source's
two-artifact structure — treated as one logical unit, per the corpus's own stated test intent.

## 2. Source and Target Systems

Source: inbound message. Target: `SimpleStockQuoteService` (assumed — not explicit in this
sample).

## 3. Trigger

Process 1: Frends **HTTP Trigger**. Process 2: Frends **Queue Trigger** (if Frends supports
queue-triggered consumption natively — needs confirmation) or a **Schedule Trigger** that polls
the queue, depending on what's actually available.

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture

**Two Frends processes, explicitly linked in documentation (this plan) even though they're
separate deployable units** — directly addressing the corpus's stated concern about the AI
treating them as unrelated:
1. **Process 1 (receiver):** HTTP Trigger → **Intermediate Return** (202 Accepted, confirmed
   native mechanism per SYN-800) → publish the message to a queue (mechanism TBD — see Open
   Questions on what queuing infrastructure Frends actually integrates with) → final Return.
2. **Process 2 (forwarder):** Queue/Schedule Trigger → `HTTP.Request` → backend → log result.

## 6. Data Mapping

See `mapping.md` — pure passthrough.

## 7. Business Rules

Caller must get an immediate ack regardless of backend availability — the core guarantee this
pattern exists to provide.

## 8. Error Handling

Process 2 needs explicit retry/dead-letter handling for backend failures — since by the time this
runs, the original caller is long gone and can't be told about a failure. This is more important
here than almost any other sample in the corpus, since the entire point of the pattern is
resilience.

## 9. Logging and Monitoring

Recommend explicit "message stored," "message forwarded," "forward failed, retrying" logging —
much more visibility than the source sample's single "storing message" log line, since operational
visibility into a decoupled async pipeline matters more than into a synchronous one.

## 10. Security

Not addressed in source.

## 11. Dependencies

Depends on whatever real queue/broker technology backs "MyStore" in Frends — **this is
infrastructure the source doesn't require explicitly** (Synapse's message store needs no external
broker by default) but a Frends queue-based design likely does. This is a real infrastructure
delta, not a detail.

## 12. Flow Diagram

See `flow.mmd`.

## 13. Frends Process Breakdown

Process 1: Trigger → Return 202 → publish to queue.
Process 2: Queue/Schedule Trigger → HTTP.Request → log → (retry/dead-letter on failure).

## 14. Migration Considerations

**Key architectural difference:** Synapse's message store is an in-process construct requiring no
external broker (though durability depends on what backs it, per source-analysis.md's unknown).
The Frends-native equivalent most likely needs a real external queue/broker as infrastructure —
this is a genuine infrastructure prerequisite to flag for AIMO, not a like-for-like swap.

## 15. Testing Strategy

- Confirm Process 1 returns 202 immediately even if the backend is deliberately made unreachable.
- Confirm the message is still delivered once the backend becomes reachable again (the actual
  guarantee under test).
- Confirm a permanently-failing delivery ends up somewhere visible (dead-letter/alert), not
  silently dropped forever.

## 16. Open Questions

0. **Resolved:** the ack-then-continue mechanic (Process 1) is natively supported via Intermediate
   Return — confirmed via docs.frends.com, no longer open.
1. **Still blocking:** what queue/broker technology does Frends actually support for this pattern
   (Queue Trigger backends, Service Bus, RabbitMQ — per the available `process_add_queue_trigger`/
   `process_add_service_bus_trigger`/`process_add_rabbitmq_trigger` tools observed in this
   session)? Needs a deliberate choice, not an assumption — and needs to match whatever
   infrastructure AIMO is willing to provision.
2. Does AIMO have an existing message broker in their real estate (their core platform uses AWS —
   SQS/SNS would be the natural AWS-native choice) that this could integrate with, rather than
   introducing a new one?
3. What was the *actual* forwarding target in the real samples #700/#702 (not shown in this
   spliced file) — confirmed assumption is `SimpleStockQuoteService` per corpus convention, not
   verified in this specific file.

## 17. Assumptions

Assumed the forwarding target is the shared corpus backend (not stated in this file). Assumed some
form of external queue is an acceptable Frends-native replacement for Synapse's message store —
not yet confirmed as the *only* option.

## 18. Reusable Components (existing)

None applicable yet.

## 19. New Reusable Components (candidates)

- **`SUB-GuaranteedDeliveryForwarder`** (subprocess, candidate) — a generic "poll queue, forward,
  retry/dead-letter on failure" component, reusable across any AIMO integration needing this
  guarantee. Not built.

## 20. Readiness Assessment

**NOT READY** — Process 1's ack mechanism is now confirmed (Intermediate Return), but the pattern
as a whole is still blocked on Open Question #1 (choice of actual queue/broker technology, which is
an infrastructure decision, not a design preference). This narrows the remaining gap but doesn't
close it. Not a real AIMO integration regardless — no build without explicit approval.
