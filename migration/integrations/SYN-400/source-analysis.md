# SYN-400 — Source Analysis: Scatter-Gather (Iterate/Aggregate)

**Corpus note:** Apache Synapse public sample #400 (complex tier). Not AIMO Park's own
configuration.

## Integration Purpose

Split one inbound message into N sub-messages, send each independently to the backend, then
recombine all N responses into one outbound message. Classic fan-out/fan-in — demonstrates RFI §3
Orchestration & Process Flow and §4 Messaging & Event Handling. No flat/linear equivalent exists.

## Existing Architecture

- **Construct:** `<proxy name="SplitAggregateProxy">`.
- **Target:** `SimpleStockQuoteService`, called once per split sub-message.

## Existing Flow

1. `<inSequence>`: `<iterate expression="//m0:getQuote/m0:request" preservePayload="true" attachPath="//m0:getQuote">` — splits the inbound message into N parts by XPath (one per
   `<m0:request>` element), each routed through a nested `<target><sequence>` that sends it to the
   backend independently. `preservePayload="true"` means the original envelope structure is
   preserved around each split part (not fully replaced).
2. `<outSequence>`: `<aggregate><onComplete expression="//m0:getQuoteResponse">...<send/></onComplete></aggregate>` — waits for all N responses, recombines them using a completion-condition XPath, then sends the combined result.

## Existing Logic

- No conditional branching — the "logic" is entirely the fan-out/fan-in mechanics: split by XPath,
  process each independently and in parallel (implied by `<iterate>`'s nature, not explicit
  threading), recombine by a completion condition.
- `attachPath="//m0:getQuote"` controls where each split result gets reattached in the (implicit)
  aggregation structure — a detail that matters for correct recombination but isn't visible as
  "logic" per se.

## Existing Configuration

None beyond the XPath expressions themselves (split expression, attach path, completion
condition).

## Existing Dependencies

None beyond the shared backend, called N times per inbound message (N = number of split parts).

## Source Assets

Source behaviour identified from `sample400_iterate_aggregate.xml` only.

## Findings

- **Confirmed:** genuine scatter-gather — split, parallel-ish dispatch, gather. This is
  structurally different from every other sample in the corpus, which are all single-path (with
  branching, not fan-out).
- **Resolved (partially) via docs.frends.com (2026-09-16):** Frends' `Foreach` shape is confirmed
  **sequential-only** — it processes elements one by one, full stop. True parallelism in Frends
  exists only via **static parallel branches** (drawing multiple connections out from one shape),
  which is a *design-time-fixed* fan-out, not a *dynamic* one sized by a runtime list length. This
  means: **there is no native Frends primitive for "split into N parallel branches where N is
  determined at runtime"** — exactly what Synapse's `<iterate>` does. The correctness of the port
  is unaffected (Foreach still produces the right aggregated result), but the concurrency
  characteristic is genuinely lost — N sequential calls instead of N potentially-parallel ones.
  This is a real, confirmed capability gap for the *parallel* half of "scatter-gather," not just an
  open question.
- **Resolved:** the "aggregate" half is not a separate concern — collecting each Foreach
  iteration's result into a list and assembling the final response after the loop completes is a
  standard, native pattern (no special "wait for all" primitive needed, since Foreach itself only
  proceeds past the loop once every iteration has run).
- **Unknown:** what happens in the source if one of the N backend calls fails — no error handling
  is visible in this sample at all. A real migration must decide this explicitly (fail the whole
  batch? proceed with partial results? retry the failed one?) rather than inheriting an
  unspecified behaviour.
