# SYN-003 — Source Analysis: Reusable Registry Endpoint + Sequence

**Corpus note:** Apache Synapse public sample #3. Not AIMO Park's own configuration. Flagged by the
corpus README as the pattern most likely to trip up a naive conversion, and the one most relevant
to AIMO's stated governance gap (RFI §11: "no structures for re-use").

## Integration Purpose

Define a backend endpoint and a piece of mediation logic **once**, in a shared registry, and
reference both by key from the main flow — rather than inlining them. Demonstrates indirection /
reuse, which is exactly the capability AIMO says it lacks today.

## Existing Architecture

- **Source:** inbound message to the `main` sequence.
- **Target:** `SimpleStockQuoteService`, resolved indirectly via the named endpoint `simple`.
- **Trigger:** any inbound message.
- **Registry constructs:** `<localEntry key="version">0.1</localEntry>` (a plain config value),
  `<endpoint name="simple">` (reusable endpoint definition), `<sequence name="stockquote">`
  (reusable mediation logic).

## Existing Flow

1. Message arrives at `main`.
2. `main`'s `<in>` block sets a property `direction=incoming`, then delegates to the reusable
   sequence via `<sequence key="stockquote"/>` — **indirection**, not inlined logic.
3. Inside `stockquote`: logs a custom message (including the `version` local entry and `direction`
   property, both resolved via `get-property(...)` expressions), then sends to the endpoint
   referenced by `key="simple"` — a **second layer of indirection**.
4. `main`'s `<out>` block is a plain `<send/>` — passthrough response.

## Existing Logic

- No conditional branching; the "logic" here is entirely about *indirection*: two named,
  independently-defined constructs (`simple` endpoint, `stockquote` sequence) referenced by key
  rather than repeated inline.
- A custom-level log statement pulls two properties via XPath-style `get-property()` expressions —
  a small but real templating/expression-evaluation requirement.

## Existing Configuration

- `version` local entry: `0.1` — a registry-stored config value, analogous to a Frends Environment
  Variable.
- `simple` endpoint: `http://localhost:9000/services/SimpleStockQuoteService`.

## Existing Dependencies

- `main` depends on `stockquote` (by key).
- `stockquote` depends on `simple` (by key) and on the `version` local entry (by key).
- This is a real dependency chain, not a coincidence — exactly the kind of chain
  `architecture/dependencies.md` needs to track once real AIMO integrations are analysed.

## Source Assets

Source behaviour identified from `sample3_reusable_registry_endpoint_sequence.xml` only.

## Findings

- **Confirmed:** this sample models reuse via a registry/key-lookup mechanism, not inline
  configuration — a real Synapse governance feature.
- **Potential migration challenge (flagged by the corpus author, confirmed on inspection):** a
  naive, line-by-line conversion could easily inline the `simple` endpoint and `stockquote`
  sequence directly into the calling process, silently discarding the reuse/indirection semantics.
  If AIMO's real 126 WSO2 integrations share endpoints/sequences this way, a faithful migration
  needs to preserve that as Frends subprocesses + environment variables, not duplicate the logic
  per-integration. This is the single most important thing to verify once real AIMO exports are
  available.
- **Assumption:** `version` (a static config value) maps cleanly to a Frends Environment Variable;
  no assumption issue there.
