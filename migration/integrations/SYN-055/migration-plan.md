# SYN-055 — Migration Plan: Session-Affine Load Balancing / Failover → **Confirmed custom build, no native primitive**

**Corpus note:** based on public Synapse sample #55 (+53), not real AIMO configuration.

## 1. Integration Overview

Reproduce a 4-level endpoint tree (session affinity → load-balance → 2 failover groups → 4
addresses) in Frends. Demonstrates RFI §1/§7 — confirmed (2026-09-16, via docs.frends.com) to be
the one pattern in either PoC corpus with **no native Frends primitive at all**; any faithful port
is a genuinely custom build.

## 2. Source and Target Systems

Source: inbound message. Target: one of 4 backend addresses, selected per the topology.

## 3. Trigger

Frends HTTP Trigger (assumed).

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture

**Capability check complete (2026-09-16) — confirmed fully custom, no native shortcut exists.**
Design, now buildable in principle though still not recommended without real-world justification
(see §20):
1. Trigger receives the message.
2. A session-pinning step (Code element, keyed on whatever session identifier is confirmed real)
   determines which of the two failover groups to use. **Persisting the choice across separate
   calls now has a confirmed native mechanism: the Shared State Task** (same one resolved for
   SYN-420's caching) — store the session→group mapping there with an appropriate TTL, instead of
   the previously-open "how would this even persist" question.
3. Within the chosen group: `HTTP.Request` to member 1; on failure (`Catch` shape), retry against
   member 2 — this part uses only standard, already-available shapes.
4. On both members failing: process-level error handler returns the SOAP fault.
5. Load-balancing *across* groups (not just failover *within* a group) still needs its own
   explicit selection logic (round-robin/random) — **confirmed no native primitive for this**, must
   be hand-rolled (e.g. a simple counter in Shared State, or random selection).

## 6. Data Mapping

See `mapping.md`.

## 7. Business Rules

Session affinity — repeat calls from the same session must hit the same failover group. Mechanism
for defining "same session" is unknown from this sample.

## 8. Error Handling

Two tiers, mirroring the source: (1) single-endpoint failure within a group → transparently retry
the group's other member (survivable, no caller-visible error); (2) both group members failed →
caller-visible SOAP fault (mirrors `onError="errorHandler"`).

## 9. Logging and Monitoring

Recommend logging every failover event (which endpoint failed, which one was tried next) — the
source doesn't surface this at all, but it's exactly the kind of thing an operator needs to see in
production.

## 10. Security

Not addressed in source.

## 11. Dependencies

Depends on resolving Open Question #1 below before any concrete Task/shape list can be specified.

## 12. Flow Diagram

See `flow.mmd` — necessarily a sketch, not a confirmed design.

## 13. Frends Process Breakdown

Cannot be finalized — see §5.

## 14. Migration Considerations

**Confirmed, not suspected:** Frends has no native load-balance/failover primitive for outbound
endpoint composition (verified against docs.frends.com — Task-level retry only repeats the *same*
call; Frends' "High Availability" feature is about inbound trigger distribution across Agents, a
different axis entirely). A faithful port is a genuinely custom, non-trivial build using Shared
State for session-pinning plus hand-rolled Decision/Catch logic for group selection and failover.
This should be surfaced directly and prominently in the RFI response: if AIMO's real WSO2 estate
uses load-balance/failover endpoint composition anywhere, that's a material migration-effort
driver, not a mechanical conversion — and it's now a confirmed fact about the platform, not an
assumption.

## 15. Testing Strategy

Cannot be defined until the architecture is resolved.

## 16. Open Questions

1. **Resolved:** confirmed no native endpoint-group failover/load-balancing primitive exists —
   verified via docs.frends.com. Any implementation is a custom build.
2. What does "session" mean here in a way that's portable to Frends — a cookie, a client
   identifier header, something else? Not visible in this sample. Still open.
3. **Resolved:** Shared State Task is the confirmed native mechanism for persisting session-pinning
   state across separate process executions.
4. **Still the most important open question:** does AIMO's real estate actually use this pattern?
   Given their real backends are SaaS (Salesforce/NetSuite, which manage their own HA) rather than
   self-hosted clusters, this pattern may have no real-world counterpart in AIMO's actual migration
   scope at all — this should be confirmed with AIMO before investing further design or build
   effort here, since the effort-to-value ratio is poor if the pattern doesn't actually exist in
   their real estate.

## 17. Assumptions

None safe to make — this plan deliberately stops at a sketch rather than asserting a design that
hasn't been capability-checked.

## 18. Reusable Components (existing)

None applicable yet.

## 19. New Reusable Components (candidates)

None proposed — premature until Open Question #1 is resolved.

## 20. Readiness Assessment

**READY WITH ASSUMPTIONS, but NOT RECOMMENDED without an AIMO relevance check first.** The
capability question (#1) is now resolved — a design is buildable using Shared State + custom
Decision/Catch orchestration. However, Open Question #4 (does this pattern even exist in AIMO's
real estate) is a real-world-value question, not a technical blocker, and should be answered before
investing build effort: this is by far the highest-effort pattern in either corpus for what may be
zero real-world applicability. Not a real AIMO integration regardless — no build without explicit
approval.
