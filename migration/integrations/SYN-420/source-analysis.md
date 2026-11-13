# SYN-420 — Source Analysis: Cache Mediator

**Corpus note:** Apache Synapse public sample #420 (complex tier, part 2 of the PoC corpus). Not
AIMO Park's own configuration.

## Integration Purpose

Cache backend responses so repeated identical requests are served from memory instead of hitting
the backend again. Demonstrates RFI §1 Connectivity & Protocol Support (state that spans the
request/response boundary).

## Existing Architecture

- **Source:** inbound message to `main`.
- **Target:** `SimpleStockQuoteService`.
- **Caching layer:** in-memory, bounded to 100 entries, 20-second timeout, scoped `per-host`, using
  a DOM-hash digest generator to compute the cache key from the message.

## Existing Flow

1. `<in>`: `<cache timeout="20" scope="per-host" collector="false" hashGenerator="...DOMHASHGenerator"><implementation type="memory" maxSize="100"/></cache>` — checks whether a response for this request's digest is already cached; if so, short-circuits and returns it (implicit Synapse cache-mediator behaviour, not shown as explicit branching in the XML).
2. If not cached: falls through to `<send>` → backend.
3. `<out>`: `<cache collector="true"/>` — stores the response just received, keyed by the same digest computed on the way in, then `<send/>` returns it to the caller.

## Existing Logic

Two instances of the same mediator type (`<cache>`) playing two different roles: a **check** on the
way in, a **collector** on the way out. No explicit branching is visible in the XML — the
short-circuit-on-hit behaviour is implicit in how the Synapse cache mediator works.

## Existing Configuration

- Timeout: 20 seconds.
- Scope: `per-host`.
- Max size: 100 entries (in-memory).
- Hash generator: DOM-hash digest (computed from message content, not headers).

## Existing Dependencies

None beyond the backend itself; the cache is self-contained in-memory state, not an external
system.

## Source Assets

Source behaviour identified from `sample420_cache_mediator.xml` only.

## Findings

- **Confirmed:** request/response caching keyed by a content digest, scoped per-host, bounded size,
  fixed TTL.
- **Resolved via docs.frends.com (2026-09-16):** Frends has a native, TTL-based cross-execution
  value store — the **Shared State Task** (backed by Shared State Storage). It supports
  `AddOrUpdate`/`GetOrAdd`/`TryGetValue`/`UpdateTTL`/`TryRemove`, TTL configurable from 1 minute to
  30 days, and can be scoped process-specific or Agent-Group-wide (the latter needs a shared DB so
  multiple Agents see the same entries). This is a genuine native feature, not a workaround — and
  a real MCP tool (`process_add_shared_state`) exists to configure it. Note: `HTTP.Request`'s
  `CacheHttpClient`/`CacheHttpClients` option is HttpClient *connection* reuse, not response
  caching — don't confuse the two.
- Frends has no built-in "cache mediator" primitive with Synapse's exact digest-based
  check/collect pairing, but Shared State gives an equivalent (and more visible/maintainable)
  building block — see migration-plan.md for the proposed design.
- **Open question (unchanged):** what should the cache key actually be for a real AIMO integration?
  This sample hashes the whole message body — for real backends (e.g. NetSuite lookups) the key is
  more likely to be a specific identifier field, not a whole-message digest. Needs clarification
  once real integrations are available.
- **Resolved:** "per-host" scope maps reasonably onto Shared State's Agent-Group-wide scope option
  (with a shared DB) if cross-agent visibility is needed; process-specific scope is the simpler
  default if it isn't. No longer an open unknown — it's a scope *choice*, not a missing capability.
