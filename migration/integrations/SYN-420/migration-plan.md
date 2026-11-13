# SYN-420 — Migration Plan: Cache Mediator → Explicit Cache-Check Pattern

**Corpus note:** based on public Synapse sample #420, not real AIMO configuration.

## 1. Integration Overview

Replace Synapse's implicit check/collector cache-mediator pair with an explicit, visible
cache-lookup step in Frends. Demonstrates RFI §1 Connectivity & Protocol Support.

## 2. Source and Target Systems

Source: inbound message. Target: `SimpleStockQuoteService` (PoC), on cache miss only.

## 3. Trigger

Frends HTTP Trigger (assumed; not confirmed by this sample alone).

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture

**Resolved (2026-09-16, via docs.frends.com):** use the native **Shared State Task**
(`process_add_shared_state`) as the cache store — no longer an open capability question.

1. Trigger receives the request.
2. Compute a cache key (a Code/Expression shape — narrower than a whole-body digest if a real
   identifying field is available; see Open Questions).
3. Shared State `TryGetValue` on the key → `ExclusiveGateway`: cache hit vs. miss.
4. Hit → Return cached value directly.
5. Miss → `HTTP.Request` Task → Shared State `AddOrUpdate` (TTL = source's 20 seconds, well within
   the supported 1-minute–30-day range — note the source's 20s is actually *below* Shared State's
   minimum granularity of 1 minute, so the real TTL would need rounding up, see Open Questions) →
   Return.

## 6. Data Mapping

See `mapping.md`.

## 7. Business Rules

Cache for 20 seconds (source value — a demo constant, not necessarily a real requirement), bounded
to 100 entries.

## 8. Error Handling

Not modeled in source. Recommend the standard default error handler for the cache-miss backend
call.

## 9. Logging and Monitoring

Recommend logging cache hit/miss explicitly — an improvement over Synapse's implicit behaviour,
since "was this a cache hit" is valuable operational information that the source doesn't surface
at all.

## 10. Security

Not addressed in source. If cached responses contain any sensitive data, cache storage/TTL choices
need a security review once real backends are known.

## 11. Dependencies

None new beyond the shared backend call pattern (`SUB-StockQuoteClient`/`ENV-StockQuoteServiceUrl`
if reused).

## 12. Flow Diagram

See `flow.mmd`.

## 13. Frends Process Breakdown

Trigger → compute cache key → ExclusiveGateway (hit/miss) → [hit: Return cached] / [miss:
HTTP.Request → store → Return].

## 14. Migration Considerations

**Key difference from source:** Synapse's cache mediator pair makes the check/store behaviour
implicit and invisible in the flow diagram — a developer reading the XML has to know how the cache
mediator works to realize caching is happening at all. The proposed Frends design makes the
cache-hit/miss decision an explicit gateway, which is more maintainable (Frends Development
Principle: prefer maintainability) at the cost of one extra visible shape.

## 15. Testing Strategy

- First request for a given key → confirm cache miss, backend called, response cached.
- Second identical request within TTL → confirm cache hit, backend NOT called.
- Request after TTL expiry → confirm cache miss again.

## 16. Open Questions

1. **Resolved:** Shared State Task is the cache-store mechanism — confirmed via docs.frends.com.
2. What should the cache key actually be for a real AIMO use case — whole-message digest (as in
   the sample) or a specific business identifier?
3. **Resolved:** "per-host" maps to Shared State's Agent-Group-wide scope (needs a shared DB) vs.
   process-specific scope — a choice, not an unknown.
4. **New:** the source's 20-second TTL is below Shared State's minimum supported granularity
   (1 minute). If sub-minute caching is a real requirement, this needs a different mechanism or an
   accepted rounding-up of TTL — flag for whoever owns the real requirement.

## 17. Assumptions

Assumed HTTP trigger; assumed a whole-message digest is not the ideal real-world cache key (flagged
as an improvement opportunity, not carried forward blindly).

## 18. Reusable Components (existing)

Would reuse `SUB-StockQuoteClient`/`ENV-StockQuoteServiceUrl` if this pattern is retrofitted onto
the original 6-sample corpus.

## 19. New Reusable Components (candidates)

- **`SUB-CacheLookup`** (subprocess, candidate) — a generic "compute key → check cache → return
  hit/miss" component, reusable across any integration needing this pattern. Not built; flagged
  because caching is likely to recur across AIMO's real 126 integrations.

## 20. Readiness Assessment

**READY WITH ASSUMPTIONS** — the former blocker (cache-store mechanism) is now resolved via
Shared State Task. Remaining open items (#2 cache key choice, #4 sub-minute TTL) are ordinary
design decisions, not capability gaps. Not a real AIMO integration regardless — no build without
explicit approval.
