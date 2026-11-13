# Dependencies — AIMO Park WSO2 → Frends PoC

## Cross-pattern dependencies (within the 6-sample PoC corpus)

All 6 samples share one dependency: the same backend service
(`SimpleStockQuoteService`). This is a deliberate Apache Synapse convention (per the corpus
README), not evidence of real coupling — but it does mean that once `SUB-StockQuoteClient` and
`ENV-StockQuoteServiceUrl` exist (see `reusable-components.md`), every one of the 6 sample
equivalents should depend on them rather than re-implementing the HTTP call:

```
SYN-150, SYN-001, SYN-003, SYN-004, SYN-056, SYN-300
        └── depend on → SUB-StockQuoteClient
                                └── depend on → ENV-StockQuoteServiceUrl
```

SYN-004 additionally depends on the (candidate) `SUB-DefaultErrorHandler` for its baseline fault
path, with a scoped override for the "SUN" branch.

## Complex-tier dependencies (9-sample corpus, part 2)

```
SYN-156 (secure leg) ──depends on──> SYN-100's WS-Security branch resolution
                                            │
                                            └──depends on──> real policy_3.xml content (not available)

SYN-420 ──depends on──> Frends cache-backing mechanism (open capability question)
SYN-360 ──depends on──> a SQL/DB connectivity Task (not yet confirmed in tenant)
SYN-400 ──depends on──> Frends Foreach concurrency model confirmation (open capability question)
SYN-700 ──depends on──> a chosen queue/broker technology (infrastructure prerequisite, not yet chosen)
SYN-055 ──depends on──> confirmation of whether Frends has ANY load-balance/failover primitive
                          (open capability question — likely a genuine gap)
```

**SYN-156 is a hard blocker on SYN-100** — its secure backend call cannot be finalized until
SYN-100's WS-Security branch (UsernameToken+Timestamp vs. policy-driven signing/encryption) is
resolved. Do not plan SYN-156 as independently ready.

## Real AIMO landscape dependencies — not yet knowable

The real dependency graph across AIMO's 126 WSO2 + 228 P2P integrations (which integrations share
which backend systems — Salesforce, NetSuite, the AWS/Kubernetes/MongoDB parking platform — and in
what direction) **cannot be mapped from this PoC corpus**. This is a hard blocker for real migration
sequencing and must be rebuilt once AIMO shares actual integration assets. Record it as an open item
on the dashboard, not as an assumption here.

## Infrastructure / prerequisite dependencies flagged in the RFI Q&A

- Agent hosting decision (Frends-hosted vs. AIMO private/on-prem) is undecided — this affects every
  future integration's deployment target, not just one.
- No current API subscription workflow automation — if migrated integrations expose APIs, this is
  a prerequisite process (subscription/catalog) that doesn't exist today and would need to be
  designed once real API-exposed integrations are identified.
