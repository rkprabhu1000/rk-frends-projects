# SYN-350 — Source Analysis: Script Mediator (Embedded JavaScript)

**Corpus note:** Apache Synapse public sample #350 (complex tier). Not AIMO Park's own
configuration.

## Integration Purpose

Run custom imperative logic (JavaScript) inline in the mediation flow — both on the request path
(transform the request) and the response path (transform the response) — rather than using only
declarative mediators. Demonstrates RFI §3 Orchestration & Process Flow and §11 Governance &
Cataloguing (custom code as a governed, registry-referenced artifact).

## Existing Architecture

- **Registry:** `<registry provider="org.apache.synapse.registry.url.SimpleURLRegistry">` rooted
  at `file:repository/conf/sample/resources/`, with a 15-second cache duration — the registry
  itself is a config-as-files mechanism, separate from the `localEntry` mechanism seen in SYN-003.
- **Script artifact 1 (request path):** `<localEntry key="stockquoteScript" src="file:.../script/stockquoteTransformRequest.js"/>` — loaded once, referenced by key.
- **Script artifact 2 (response path):** referenced directly by registry path
  (`script/stockquoteTransformResponse.js`), **not** via a `localEntry` — a different reference
  mechanism than script 1, in the same file.
- **Target:** `SimpleStockQuoteService`.

## Existing Flow

1. `<in>`: `<script language="js" key="stockquoteScript" function="transformRequest"/>` runs the
   named JS function against the inbound message, then `<send/>` to the backend.
2. `<out>`: `<script language="js" key="script/stockquoteTransformResponse.js" function="transformResponse"/>` runs a second JS function against the response, then `<send/>` to the caller.

## Existing Logic

The actual JavaScript logic (`transformRequest`/`transformResponse` function bodies) is **not
included in this corpus** — only the XML wiring that invokes them. This is a hard visibility gap:
we know *that* custom logic runs at two points, but not *what* it does.

## Existing Configuration

- Registry root: `file:repository/conf/sample/resources/` (Synapse-runtime-relative).
- Two distinct reference styles for what is conceptually the same kind of artifact
  (`localEntry`-then-key vs. direct registry-path) — worth flagging as an inconsistency in the
  source pattern itself, not just noise.

## Existing Dependencies

Depends on both `.js` files being present in the registry at runtime; depends on whatever
libraries/globals the Synapse JS engine (Rhino/Nashorn-era, historically) exposes to those
scripts — unknown from this sample.

## Source Assets

Source behaviour identified from `sample350_script_mediator.xml` only. The two referenced
JavaScript files (`stockquoteTransformRequest.js`, `stockquoteTransformResponse.js`) were **not**
included in this corpus — their actual transformation logic is unknown.

## Findings

- **Confirmed:** two distinct custom-script hook points (request and response), each invoking a
  named function, each referenced slightly differently.
- **Critical unknown:** the actual script logic. Without it, no faithful migration plan can specify
  what the equivalent Frends Code element should actually do — only *where* it goes in the flow.
- **Potential migration challenge:** JavaScript-specific APIs/idioms used inside the scripts (if
  any) don't have a 1:1 translation to C# (Frends' native scripting language for Code elements) —
  a real migration would need the actual script content to assess this, not just the wiring.
- **Governance note:** the two different reference styles for conceptually similar script
  artifacts is itself the kind of inconsistency AIMO's real 126-integration estate likely has at
  scale — worth calling out as a "clean this up during migration" opportunity, not something to
  carry forward as-is.
