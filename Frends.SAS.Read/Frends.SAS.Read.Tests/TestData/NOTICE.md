# Test fixture provenance

`sample.sas7bdat` and `dates.sas7bdat` are copies of `test1.sas7bdat` and
`datetime.sas7bdat` from the [pandas](https://github.com/pandas-dev/pandas) project's
SAS I/O test suite (`pandas/tests/io/sas/data/`), used here only as small (128 KB)
non-sensitive binary fixtures to exercise the SAS.Read task's parser integration.

pandas is licensed under BSD-3-Clause. Copyright (c) 2008-2011 AQR Capital Management,
LLC, Lambda Foundry, Inc. and PyData Development Team; Copyright (c) 2011-2026, Open
source contributors.

`dates_binary.sas7bdat` (196 KB) is a copy of `dates_binary.sas7bdat` from the
[cpp-sas7bdat](https://github.com/olivia76/cpp-sas7bdat) project's test suite
(`test/data_reikoch/`), used here to exercise SAS's BIN/RDC internal compression
scheme (`SASYZCR2`), which none of the other fixtures here use (they use RLE/CHAR
compression or no compression).

cpp-sas7bdat is licensed under Apache License 2.0. Copyright (c) Olivia Quinet.
