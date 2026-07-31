# Changelog

## [1.0.3] - 2026-07-31

### Fixed
- Added further day-precision SAS date formats that `SasReader` doesn't classify as
  dates, using the same supplemental conversion path (SAS's standard day-count
  epoch) added for `MINGUO` in 1.0.2: `NENGO` (Japanese imperial era calendar),
  `QTR`, `QTRR`, `WEEKV`, and the `YYQ`/`YYQR` family of year-quarter formats. Found
  by diffing `SasReader`'s recognized-format tables against pandas'
  `sas_date_formats` list, which is itself sourced from `parso`'s
  `SasFileConstants.java` - the same upstream library `SasReader` is ported from, so
  this is a genuine coverage gap in the .NET port rather than a difference between
  ecosystems.

## [1.0.2] - 2026-07-31

### Fixed
- `MINGUO`-formatted (Taiwanese/ROC calendar) date columns are now converted to ISO
  8601 date strings under `DateOutputFormat=Iso8601String`, instead of being left as
  a raw SAS numeric value. `SasReader` doesn't classify `MINGUO` as a date format, so
  it never materializes it as a `DateTime`; confirmed via cross-check against
  `pandas.read_sas` that the underlying value follows the standard SAS day-count
  epoch, and added it to a small supplemental recognized-format list.

## [1.0.1] - 2026-07-30

### Fixed
- Retargeted from `net10.0` to `net8.0`. The net10.0 build imported into the Frends
  portal as a package with zero discoverable Process Tasks (no error surfaced) -
  net8.0 matches every other installed task on the tenant and imports correctly.

## [1.0.0] - 2026-07-30

### Added
- Initial release of Frends.SAS.Read.
- `SAS.Read` task: reads a .sas7bdat file (from a file path or in-memory bytes) and
  converts it to structured JSON - column metadata plus data rows keyed by column
  name. Handles SAS internal RLE/RDC compression and encoding detection.
