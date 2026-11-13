# SYN-360 — Migration Plan: DB-Enriched Routing → SQL Task + Simplified Decision

**Corpus note:** based on public Synapse sample #360, not real AIMO configuration.

## 1. Integration Overview

Look up a value in a database, branch on found/not-found, and reproduce the source's two distinct
fault paths (business "not found" vs. technical error). Demonstrates RFI §1 + §3, and is a good
example of "improve, don't blindly recreate."

## 2. Source and Target Systems

Source: inbound request. External: Derby DB (PoC) / real AIMO database (unknown). Target:
`SimpleStockQuoteService`.

## 3. Trigger

Frends HTTP Trigger (assumed).

## 4. Current Integration Flow

See `source-analysis.md`.

## 5. Proposed Frends Architecture

1. Trigger receives the request.
2. A **SQL Task** (exact Task TBD — needs `list_tasks`/`search_task_packages` check for what's
   available/imported in the tenant, e.g. a generic SQL Server/Postgres/MySQL task, or Derby-
   specific if that's ever real) runs the parameterized lookup.
3. `ExclusiveGateway`: found vs. not found (**collapsed from the source's 3-way `c1`/`c2`/`c3` +
   default into a 2-way decision** — see Migration Considerations).
4. Found → log company ID → `HTTP.Request` → Return.
5. Not found → log → Return a structured fault response (business rule, not a technical error).
6. Process-level default error handler for genuine technical failures (DB unreachable, backend
   unreachable) — separate from the "not found" business path, mirroring the source's two distinct
   fault mechanisms.

## 6. Data Mapping

See `mapping.md`.

## 7. Business Rules

"Not found" is a valid business outcome (return a specific fault), not a technical error — keep
these two paths distinct in the Frends design, exactly as the source does.

## 8. Error Handling

Two-tier, mirroring the source: (1) business-rule "not found" → explicit fault response; (2)
unhandled technical error (DB or backend unreachable) → process-level default error handler,
functionally equivalent to `onError="myFaultHandler"`.

## 9. Logging and Monitoring

Preserve "looking up from the database" and per-branch logging; simplify to one log statement for
the (now-unified) found branch rather than 3 near-duplicate ones.

## 10. Security

**Preserve parameterized SQL** — the source already does this correctly; a Frends Code/SQL task
must bind the symbol as a parameter, never concatenate it into the query string, to avoid
introducing a SQL-injection risk not present in the source.

## 11. Dependencies

A SQL/database connectivity Task — not yet confirmed available/imported in this tenant. Also
depends on `SUB-StockQuoteClient`/`ENV-StockQuoteServiceUrl` for the backend call.

## 12. Flow Diagram

See `flow.mmd`.

## 13. Frends Process Breakdown

Trigger → SQL Task (parameterized lookup) → ExclusiveGateway (found/not found) → [found: log →
HTTP.Request → Return] / [not found: log → Return fault] → process-level default error handler for
unhandled failures.

## 14. Migration Considerations

**Deliberate simplification, not a 1:1 port:** the source's `c1`/`c2`/`c3` cases are functionally
identical (same action, different log text) — the Frends design collapses them to one "found"
branch with the company ID logged dynamically, rather than replicating 3 near-duplicate branches.
This is exactly the "recommend improvements rather than blindly recreating" principle — documented
here per Continuous Traceability: what the original did (3 near-identical branches), what Frends
does (1 branch, dynamic logging), why it differs (DRY, no business-rule difference between the 3
cases), why it's safe (identical externally observable behaviour for `c1`/`c2`/`c3` inputs).

## 15. Testing Strategy

- Lookup returns a known company ID → confirm found-branch behaviour (backend call + response).
- Lookup returns no row → confirm not-found fault response, no backend call.
- DB unreachable → confirm the technical-error path fires (process-level default handler), not the
  not-found path — these must stay distinguishable.

## 16. Open Questions

1. What database technology and connectivity does AIMO's real use of this pattern (if any)
   actually need? Derby is almost certainly a Synapse-demo artifact — AIMO's real stack per the RFI
   Q&A is AWS/Kubernetes/MongoDB, which is a materially different connectivity story (MongoDB
   query, not parameterized SQL).
2. Is a suitable SQL/DB Task already imported in the `rktestfrends` tenant? Check via `list_tasks`
   before any build.

## 17. Assumptions

Assumed the 3-way-to-1-way branch collapse is safe because the 3 cases are observably identical
except for log text — verified by direct inspection of the sample XML, not an inference.

## 18. Reusable Components (existing)

Would reuse `SUB-StockQuoteClient`/`ENV-StockQuoteServiceUrl`.

## 19. New Reusable Components (candidates)

- **`SUB-DbLookupWithFallback`** (subprocess, candidate) — a generic "parameterized lookup → found
  or not-found fault" pattern, reusable if AIMO's real estate has similar DB-gated routing
  elsewhere. Not built.

## 20. Readiness Assessment

**READY WITH ASSUMPTIONS** for the PoC conversion exercise, contingent on confirming a suitable SQL
Task exists in the tenant. Not a real AIMO integration — no build without explicit approval, and the
real database technology (Open Question #1) must be confirmed before this pattern is applied to any
actual AIMO integration.
