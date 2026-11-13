# SYN-004 — Source Analysis: Error Handling

**Corpus note:** Apache Synapse public sample #4. Not AIMO Park's own configuration.

## Integration Purpose

Demonstrate Synapse's two-tier fault-handling model: a landscape-wide default `fault` sequence,
overridable per-branch via a named `onError` sequence. Demonstrates RFI §7 Error Handling &
Resilience.

## Existing Architecture

- **Source:** inbound `getQuote` request carrying a stock `symbol`.
- **Target:** `SimpleStockQuoteService` for IBM; a deliberately bogus endpoint (`key="bogus"`) for
  MSFT; a separate `sunPort`-keyed endpoint (undefined in this sample, presumably elsewhere in the
  full sample set) for SUN.
- **Trigger:** any inbound message.

## Existing Flow

1. Message arrives at `main`.
2. `<switch>` on `//m0:getQuote/m0:request/m0:symbol` (an XPath into the SOAP body, not a header —
   different from SYN-001's property-based filter):
   - `IBM` → send directly to the real backend address.
   - `MSFT` → send to endpoint key `bogus` — **intentionally broken**, to demonstrate the default
     fault path.
   - `SUN` → delegate to a separate named sequence, `sunSequence`.
3. `sunSequence` has its own `onError="sunErrorHandler"` attribute — meaning failures inside it are
   caught by `sunErrorHandler`, not the global default `fault` sequence.
4. Both `fault` (default) and `sunErrorHandler` (override) log a custom message including
   `get-property('ERROR_MESSAGE')`, then `<drop/>`.

## Existing Logic

- Multi-case switch (3 branches) on message body content (XPath), not a header/property.
- Two-tier error handling: default (`fault`) vs. named override (`sunErrorHandler`), selected by
  which sequence the error occurred in, not by error type/content.

## Existing Configuration

- Real backend URL for IBM: `http://localhost:9000/services/SimpleStockQuoteService`.
- `bogus` endpoint key (MSFT branch) — undefined/deliberately broken in this sample, to trigger the
  default fault path for demonstration.
- `sunPort` endpoint key (SUN branch, via `sunSequence`) — also undefined in this sample file alone
  (likely defined in a sibling sample not included in this corpus).

## Existing Dependencies

`main` depends on `sunSequence` (by key); `sunSequence` depends on `sunErrorHandler` (by
`onError` attribute) and on an undefined `sunPort` endpoint.

## Source Assets

Source behaviour identified from `sample4_error_handling.xml` only.

## Findings

- **Confirmed:** two-tier error handling — default fault sequence, with a named per-sequence
  override — and it's scoped by *which sequence failed*, not by error type.
- **Unknown / gap in this sample alone:** the `bogus` and `sunPort` endpoint keys are referenced but
  not defined in this file — likely defined in the full Apache Synapse sample set (samples share a
  common resource file) but not present in this 6-file corpus. Treat both as intentionally-broken
  stand-ins for demonstration purposes, not a real endpoint to migrate.
- **Potential migration challenge:** Frends' error-handling model is not naturally scoped
  "per-sequence" the way Synapse's `onError` attribute is — it's closer to scoped `Catch` shapes
  around specific branches. The proposed design (§8 in the migration plan) reflects that translation
  rather than a literal one.
