using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Frends.SAS.Read.Definitions;
using Frends.SAS.Read.Helpers;
using NUnit.Framework;

namespace Frends.SAS.Read.Tests;

public class SASTests
{
    /// <summary>
    /// Reads whatever file is set via SAS_TEST_FILE_PATH in .env and dumps the full
    /// JSON result to the test output, so a real customer file can be eyeballed
    /// without deploying to a Frends agent first. Skipped when not configured.
    /// </summary>
    [Test]
    public async Task Read_LiveFile_DumpsFullJsonResult()
    {
        if (string.IsNullOrWhiteSpace(TestBase.LiveFilePath))
            Assert.Ignore("SAS_TEST_FILE_PATH not configured - skipping live file test.");

        var input = new Input { SourceType = SasInputType.File, FilePath = TestBase.LiveFilePath };
        var options = new Options { ThrowErrorOnFailure = false };

        var result = await SAS.Read(input, options, CancellationToken.None);

        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));

        Assert.That(result.Success, Is.True, result.Error?.Message);
    }

    /// <summary>
    /// dates_binary.sas7bdat uses SAS's BIN/RDC internal compression (SASYZCR2),
    /// unlike every other fixture here which uses RLE/CHAR compression or none at
    /// all - this is the only regression coverage for that code path.
    /// </summary>
    [Test]
    public async Task Read_RdcCompressedFile_ParsesCorrectly()
    {
        var input = new Input { SourceType = SasInputType.File, FilePath = TestBase.RdcCompressedFilePath };
        var options = new Options();

        var result = await SAS.Read(input, options, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RowCount, Is.EqualTo(19));
        Assert.That(result.Rows[0]["dt"], Is.EqualTo("1959-12-30T23:59:59"));
        Assert.That(result.Rows[0]["missings"], Is.EqualTo(1.0));
        Assert.That(result.Rows[1]["missings"], Is.Null);
        Assert.That(result.Rows[18]["seconds"], Is.EqualTo(2485773565.0));
    }

    [Test]
    public async Task Read_FromFilePath_ReturnsColumnsAndRows()
    {
        var input = new Input { SourceType = SasInputType.File, FilePath = TestBase.SampleFilePath };
        var options = new Options();

        var result = await SAS.Read(input, options, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RowCount, Is.EqualTo(10));
        Assert.That(result.Rows, Has.Count.EqualTo(10));
        Assert.That(result.Columns, Is.Not.Null);
        Assert.That(result.Columns, Has.Count.GreaterThan(0));
    }

    [Test]
    public async Task Read_FromBytes_MatchesFilePathResult()
    {
        var bytes = await File.ReadAllBytesAsync(TestBase.SampleFilePath);
        var input = new Input { SourceType = SasInputType.Bytes, Data = bytes };
        var options = new Options();

        var result = await SAS.Read(input, options, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.RowCount, Is.EqualTo(10));
    }

    [Test]
    public async Task Read_MaxRows_LimitsRowCount()
    {
        var input = new Input { SourceType = SasInputType.File, FilePath = TestBase.SampleFilePath };
        var options = new Options { MaxRows = 3 };

        var result = await SAS.Read(input, options, CancellationToken.None);

        Assert.That(result.RowCount, Is.EqualTo(3));
        Assert.That(result.Rows, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task Read_IncludeColumnMetadataFalse_OmitsColumns()
    {
        var input = new Input { SourceType = SasInputType.File, FilePath = TestBase.SampleFilePath };
        var options = new Options { IncludeColumnMetadata = false };

        var result = await SAS.Read(input, options, CancellationToken.None);

        Assert.That(result.Columns, Is.Null);
    }

    [Test]
    public async Task Read_Iso8601DateFormat_ConvertsDateColumnToIsoString()
    {
        var input = new Input { SourceType = SasInputType.File, FilePath = TestBase.DatesFilePath };
        var options = new Options { DateOutputFormat = SasDateOutputFormat.Iso8601String };

        var result = await SAS.Read(input, options, CancellationToken.None);

        var firstRow = result.Rows[0];
        Assert.That(firstRow["Date2"], Is.TypeOf<string>());
        Assert.That((string)firstRow["Date2"], Does.Match(@"^\d{4}-\d{2}-\d{2}$"));
    }

    [Test]
    public async Task Read_RawSasNumericValue_LeavesDateColumnAsNumber()
    {
        var input = new Input { SourceType = SasInputType.File, FilePath = TestBase.DatesFilePath };
        var options = new Options { DateOutputFormat = SasDateOutputFormat.RawSasNumericValue };

        var result = await SAS.Read(input, options, CancellationToken.None);

        var firstRow = result.Rows[0];
        Assert.That(firstRow["Date2"], Is.TypeOf<double>());
    }

    [Test]
    public async Task Read_Iso8601DateFormat_ConvertsMinguoColumnToIsoString()
    {
        var input = new Input { SourceType = SasInputType.File, FilePath = TestBase.DatesFilePath };
        var options = new Options { DateOutputFormat = SasDateOutputFormat.Iso8601String };

        var result = await SAS.Read(input, options, CancellationToken.None);

        // Cross-checked against pandas.read_sas on the same file: row 0 -> 1912-01-01,
        // row 1 -> 1960-01-01 (the SAS epoch itself, i.e. raw value 0).
        Assert.That(result.Rows[0]["Taiw"], Is.EqualTo("1912-01-01"));
        Assert.That(result.Rows[1]["Taiw"], Is.EqualTo("1960-01-01"));
    }

    [Test]
    public async Task Read_RawSasNumericValue_LeavesMinguoColumnAsNumber()
    {
        var input = new Input { SourceType = SasInputType.File, FilePath = TestBase.DatesFilePath };
        var options = new Options { DateOutputFormat = SasDateOutputFormat.RawSasNumericValue };

        var result = await SAS.Read(input, options, CancellationToken.None);

        Assert.That(result.Rows[0]["Taiw"], Is.EqualTo(-17532.0));
    }

    // No public .sas7bdat fixture uses these formats (QTR/YYQ*/NENGO/WEEKV are niche
    // enough that none of the available public test files exercise them), so these
    // exercise SasTemporalHelper directly via InternalsVisibleTo rather than via a
    // real file. 20513.0 is the same raw day-count value verified against
    // pandas.read_sas on dates.sas7bdat's Taiw (MINGUO) column, which resolves to
    // 2016-02-29 - reused here since every format in this list shares the same
    // day-count epoch.
    [TestCase("MINGUO")]
    [TestCase("NENGO")]
    [TestCase("QTR")]
    [TestCase("QTRR")]
    [TestCase("WEEKV")]
    [TestCase("YYQ")]
    [TestCase("YYQC")]
    [TestCase("YYQD")]
    [TestCase("YYQP")]
    [TestCase("YYQS")]
    [TestCase("YYQN")]
    [TestCase("YYQR")]
    [TestCase("YYQRC")]
    [TestCase("YYQRD")]
    [TestCase("YYQRP")]
    [TestCase("YYQRS")]
    [TestCase("YYQRN")]
    public void ToJsonValue_SupplementalDateFormats_ConvertUsingSasEpoch(string formatName)
    {
        var result = SasTemporalHelper.ToJsonValue(20513.0, formatName, convertDates: true);

        Assert.That(result, Is.EqualTo("2016-02-29"));
    }

    [Test]
    public void ToJsonValue_UnrecognizedFormat_LeavesRawValueUnchanged()
    {
        var result = SasTemporalHelper.ToJsonValue(20513.0, "COMMA", convertDates: true);

        Assert.That(result, Is.EqualTo(20513.0));
    }

    [Test]
    public void ToJsonValue_ConvertDatesFalse_LeavesSupplementalFormatAsRawValue()
    {
        var result = SasTemporalHelper.ToJsonValue(20513.0, "NENGO", convertDates: false);

        Assert.That(result, Is.EqualTo(20513.0));
    }

    [Test]
    public void Read_MissingFilePath_ThrowsArgumentException()
    {
        var input = new Input { SourceType = SasInputType.File, FilePath = string.Empty };
        var options = new Options();

        Assert.ThrowsAsync<System.ArgumentException>(new AsyncTestDelegate(() => SAS.Read(input, options, CancellationToken.None)));
    }

    [Test]
    public async Task Read_ThrowErrorOnFailureFalse_ReturnsErrorInResult()
    {
        var input = new Input { SourceType = SasInputType.File, FilePath = string.Empty };
        var options = new Options { ThrowErrorOnFailure = false };

        var result = await SAS.Read(input, options, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.Not.Null);
    }
}
