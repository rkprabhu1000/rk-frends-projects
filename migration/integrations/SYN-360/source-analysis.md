# SYN-360 — Source Analysis: DB-Enriched Routing

**Corpus note:** Apache Synapse public sample #360 (complex tier). Not AIMO Park's own
configuration.

## Integration Purpose

Look up a value from an external database, then branch the flow based on the looked-up value
(not the original inbound message content). Demonstrates RFI §1 Connectivity & Protocol Support
(external DB state) combined with §3 Orchestration & Process Flow (branching).

## Existing Architecture

- **Source:** inbound `getQuote` request with a stock `symbol`.
- **External DB:** Apache Derby via JDBC (`jdbc:derby://localhost:1527/synapsedb`), connection
  pool defined inline in the `<dblookup>` mediator.
- **Target:** `SimpleStockQuoteService`, for 3 of 4 branches; a fault response for the 4th.
- **Error handling:** `onError="myFaultHandler"` at the `main` sequence level — a SOAP fault
  response constructed via `<makefault>`, not just a log-and-drop like SYN-004's default fault.

## Existing Flow

1. Log: "Looking up from the Database."
2. `<dblookup>`: runs `select * from company where name = ?`, binding the XPath-extracted
   `symbol` as the parameter, storing the `id` column as property `company_id`.
3. `<switch source="get-property('company_id')">`: 4-way branch —
   - `c1`, `c2`, `c3`: each logs the company ID, then sends to the backend (identical action per
     case, just different log text — a real DRY opportunity).
   - `default`: logs "Unrecognized Company ID," constructs a SOAP fault via `<makefault>`, sends
     it, drops.
4. Any unhandled error anywhere in `main` is caught by `myFaultHandler`, which also constructs a
   `<makefault>` response (a generic `Receiver`-code fault) rather than a log-and-drop.

## Existing Logic

- External DB lookup feeding a switch — the branch decision depends on *derived* state, not the
  inbound message directly.
- 3 of the 4 switch cases are functionally identical (same action, different log string) — a
  strong signal that the "real" business rule is just "found vs. not found," with the specific
  company ID being incidental to logging only.
- Two distinct fault-construction mechanisms in one file: the `default` case's inline
  `<makefault>` and the sequence-level `onError` handler's `<makefault>` — structurally similar but
  triggered differently (business-rule "not found" vs. unhandled technical error).

## Existing Configuration

- DB connection: Derby JDBC, `synapsedb` database, credentials `synapse`/`synapse` (sample
  credentials — clearly not production-representative).
- SQL: parameterized (`?` placeholder), not string-concatenated — good practice already present in
  the source, worth preserving.

## Existing Dependencies

Depends on the Derby database being reachable and containing a `company` table with `name`/`id`
columns.

## Source Assets

Source behaviour identified from `sample360_dblookup_switch.xml` only.

## Findings

- **Confirmed:** DB lookup → branch on looked-up value → 3 functionally-identical "found" branches
  + 1 "not found" branch with an explicit SOAP fault + a separate technical-error fault handler.
- **Simplification opportunity:** the 3 near-identical `c1`/`c2`/`c3` cases should almost certainly
  collapse into a single "found" branch in the Frends design (log the company ID generically, one
  send), rather than being ported as 3 separate branches — this is exactly the kind of "don't
  blindly recreate" case the migration-architect approach calls for.
- **Potential migration challenge:** Frends needs a genuine SQL/DB task
  (e.g. a `SQL`/database connector) configured against whatever real database backs this in AIMO's
  actual estate — not verified against `list_tasks` yet; flag as a dependency to check before
  building.
- **Potential migration challenge:** the parameterized SQL is good practice already — preserve
  parameterization in the Frends equivalent (never string-concatenate the symbol into the SQL) to
  avoid introducing a SQL-injection risk that wasn't present in the source.
- **Unknown:** whether AIMO's real integrations use Derby specifically, or some other DB (likely,
  given their real stack is AWS/Kubernetes/MongoDB per the RFI Q&A — Derby is almost certainly a
  Synapse-demo-only artifact, not representative of AIMO's real database technology).
