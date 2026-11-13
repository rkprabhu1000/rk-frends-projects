# SYN-156 — Source Analysis: Cross-Sequence Continuation + Enrich Comparison

**Corpus note:** Apache Synapse public sample #156 (complex tier). Not AIMO Park's own
configuration. Flagged by the corpus README as the most structurally tangled sample in either
tier.

## Integration Purpose

Call two backends (a "secure" and a "simple" stock quote service) in sequence, compare their
results, and return whichever response corresponds to the better value — using WS-Security on the
first call (reusing SYN-100's policy). Demonstrates RFI §3 Orchestration & Process Flow and §11
Governance & Cataloguing (three separate top-level definitions chained together).

## Existing Architecture

Three separate top-level constructs, chained via `<send receive="...">` (message-relay
continuation — the response of one call resumes execution in a *different*, explicitly-named
sequence, not back in the caller):

1. **`StockQuoteProxy`** (proxy): entry point.
2. **`SimpleServiceSeq`** (named sequence): resumes after the proxy's first call completes.
3. **`ClientOutSeq`** (named sequence): resumes after `SimpleServiceSeq`'s call completes; this is
   where the final comparison and response happen.

## Existing Flow

1. **`StockQuoteProxy.inSequence`:**
   - `<enrich source="body" target="property:REQUEST">` — saves a copy of the original request
     body into a property (so it can be replayed later against the second backend).
   - `<send receive="SimpleServiceSeq">` to the `secure` endpoint (WS-Security-protected, same
     `sec_policy` as SYN-100) — **when this response arrives, execution continues in
     `SimpleServiceSeq`, not back in the proxy.**
   - `outSequence`: `<drop/>` (the proxy itself never sees the final response — by design, since
     continuation happens elsewhere).
2. **`SimpleServiceSeq`** (resumed with the secure backend's response as the current message):
   - Extracts `SECURE_SER_AMT` from the response, logs it.
   - `<enrich source="body" target="property:SecureService_Res">` — saves the *secure* backend's
     response into a property (so it survives being overwritten by the next call's response).
   - `<enrich source="property:REQUEST" target="body">` — **restores the original saved request**
     as the current message body, discarding the secure response as the active message.
   - `<send receive="ClientOutSeq">` to the plain `SimpleStockQuoteService` endpoint (no security)
     — sends the *original* request a second time, to a *different* backend.
3. **`ClientOutSeq`** (resumed with the simple backend's response):
   - Extracts `SIMPLE_SER_AMT`, logs it.
   - `<enrich source="body" target="property:SimpleService_Res">` — saves the simple backend's
     response too.
   - `<filter xpath="fn:number(SIMPLE_SER_AMT) > fn:number(SECURE_SER_AMT)">`: if the simple
     backend's value is higher, log that fact and **keep the current body (the simple response)
     as-is**; else, log the opposite and `<enrich source="property:SecureService_Res" target="body">` — **swap the current body for the previously-saved secure response instead.**
   - `<send/>` — return whichever body ended up being the "winner" back to the original caller.

## Existing Logic

- **Five separate `<enrich>` operations**, each saving or restoring body↔property state at a
  specific point — this is the mechanism that makes the whole multi-hop, multi-backend comparison
  possible without a single sequence needing to hold multiple values in scope at once (Synapse's
  "current message" model only has one body at a time).
- A genuine business rule: call two backends with the same logical request, compare a numeric
  field from each response, return whichever is better.
- Three-hop control flow across three separate top-level definitions, connected only by
  `receive="..."` attributes — nothing about the XML structure itself makes the chain visually
  obvious; a reader has to trace the `receive` attributes to reconstruct the actual execution
  order.

## Existing Configuration

- `secure` endpoint: `http://localhost:9000/services/SecureStockQuoteService`, WS-Security-protected
  (same `sec_policy` as SYN-100).
- `SimpleStockQuoteService` endpoint: `http://localhost:9000/services/SimpleStockQuoteService`,
  unprotected.

## Existing Dependencies

Depends on `sec_policy` (same external policy document as SYN-100 — same unresolved unknown about
its actual content). Depends on both backends being called with the *same* original request
(hence the `REQUEST` property save/restore).

## Source Assets

Source behaviour identified from `sample156_enrich_multisequence_routing.xml` only. Independently
re-verified against the live synapse.apache.org page on 2026-09-16 per the complex-tier README's
verification note.

## Findings

- **Confirmed:** a genuine two-backend comparison pattern, using enrich-based state save/restore to
  work around Synapse's single-current-message model, spread across 3 separately-defined
  sequences connected by `receive` attributes rather than being visually linear in one place.
- **Potential migration challenge:** Frends doesn't have Synapse's "one current message, save/restore via enrich" constraint at all — Frends process variables can hold multiple named values
  simultaneously throughout a process's execution. This means the Frends equivalent should **not**
  replicate the 5 enrich operations literally; it should simply call both backends and hold both
  results as separate variables, then compare — a genuine simplification opportunity, not a
  faithful port of the enrich mechanics.
- **Same open dependency as SYN-100:** the `secure` backend call still depends on resolving the
  WS-Security question (Branch A vs. B, per SYN-100's corrected findings) — this integration should
  not be planned as READY until SYN-100's branch is resolved for the real use case.
- **Potential migration challenge:** the 3-way sequence-continuation-via-`receive` structure is the
  clearest example in either corpus of "logic split across files, only reconstructable by tracing
  attributes" — exactly the governance/traceability gap the corpus README calls out. A real
  migration of AIMO's actual estate should watch for this same pattern (logic spread across
  multiple WSO2 artifacts linked only by non-obvious references) as a red flag requiring careful
  reconstruction before any Frends design is attempted.
