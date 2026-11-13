# SYN-055 — Source Analysis: Session-Affine Load Balancing Over Failover Groups

**Corpus note:** Apache Synapse public sample #55 (complex tier, incorporating sample #53's
failover-group structure per the corpus README). Not AIMO Park's own configuration.

## Integration Purpose

Distribute load across backend instances while tolerating individual instance failure, with
session affinity (repeat requests from the same client session stick to the same backend group).
Demonstrates RFI §1 Connectivity & Protocol Support (endpoint composition) and §7 Error Handling &
Resilience.

## Existing Architecture

Four-level endpoint nesting, all inline in one `<send>`:
```
<endpoint>
  <session type="simpleClientSession"/>          <!-- session affinity -->
  <loadbalance>                                    <!-- distributes across... -->
    <endpoint><failover>                            <!-- ...two failover groups -->
      <endpoint><address uri="...:9001/LBService1"/></endpoint>
      <endpoint><address uri="...:9002/LBService1"/></endpoint>
    </failover></endpoint>
    <endpoint><failover>
      <endpoint><address uri="...:9003/LBService1"/></endpoint>
      <endpoint><address uri="...:9004/LBService1"/></endpoint>
    </failover></endpoint>
  </loadbalance>
</endpoint>
```
Four distinct backend addresses total (`:9001`–`:9004`), each with `<enableAddressing/>`
(WS-Addressing enabled per-endpoint).

## Existing Flow

1. `<in>`: `<send>` to the composed endpoint above; `<drop/>` immediately after (fire-and-forget
   shape at the `main` level, similar in spirit to SYN-800's Resource 2, though for a different
   reason here — this sample's `main` sequence doesn't wait to process a response itself).
2. `<out>`: plain passthrough `<send/>`.
3. On any send failure, `onError="errorHandler"` (sequence-level) fires: constructs and sends a
   SOAP fault ("COULDN'T SEND THE MESSAGE TO THE SERVER"), a single shared fault path for the
   entire composed endpoint tree — no distinction between "one instance down" (survivable) vs.
   "everything down" (real failure); Synapse's load-balance/failover composition already absorbs
   the survivable case internally, so `errorHandler` only fires when the whole tree is exhausted.

## Existing Logic

No business logic — purely endpoint-topology composition. The "logic" is entirely in how the tree
is structured: load-balance picks one of two failover *groups*, and each group internally retries
across its 2 members before that group is considered failed.

## Existing Configuration

Four backend addresses (`:9001`–`:9004`), grouped 2+2 into two failover units, both load-balanced
over.

## Existing Dependencies

None beyond the four backend addresses themselves.

## Source Assets

Source behaviour identified from `sample55_session_loadbalance_failover.xml` only (per the corpus
README, this splices in sample #53's failover-group structure, verified against the live
synapse.apache.org page on 2026-09-16).

## Findings

- **Confirmed:** a genuine endpoint *tree*, not a flat list — session affinity wraps a
  load-balancer, which wraps two failover groups, each with 2 members. The corpus README's stated
  test ("does the AI treat this as a tree, not a flat list") is a real and valid concern; a naive
  conversion could easily flatten this into "4 possible backends, pick one somehow" and lose the
  grouping semantics (which pairs form a failover unit, and which units get load-balanced).
- **Confirmed as a real capability gap via docs.frends.com (2026-09-16), not just a suspicion:**
  Frends has **no native Task option for outbound load-balancing or automatic failover across
  multiple backend endpoints.** What Frends does provide: (a) Task-level "Retry on failure" —
  retries the *same* call, up to 10 times with exponential backoff, not a different endpoint; (b)
  Frends' own "High Availability" feature load-balances *incoming triggers across Agents* — a
  completely different axis (inbound trigger distribution, not outbound endpoint selection) and
  must not be confused with this pattern. Reproducing the source's 4-level endpoint tree faithfully
  would require fully custom orchestration (Decision/Catch/loop shapes hand-rolling group
  selection, retry-the-next-endpoint, and session-pinning) — this is now a **confirmed gap**, not
  an open question.
- **Unknown:** what "session affinity" (`simpleClientSession`) actually keys on — likely a
  client-session identifier/cookie, not visible in this sample. Without knowing the real session
  key, a faithful Frends port can't be fully specified.
