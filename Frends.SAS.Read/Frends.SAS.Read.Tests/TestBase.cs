using System;
using System.IO;
using dotenv.net;

namespace Frends.SAS.Read.Tests;

/// <summary>
/// Shared test setup: resolves paths to the .sas7bdat fixtures under TestData/, and
/// loads the optional .env file used to point live tests at a real customer file.
/// </summary>
public static class TestBase
{
    static TestBase() => DotEnv.Load();

    public static string SampleFilePath => Path.Combine(AppContext.BaseDirectory, "TestData", "sample.sas7bdat");

    public static string DatesFilePath => Path.Combine(AppContext.BaseDirectory, "TestData", "dates.sas7bdat");

    public static string RdcCompressedFilePath => Path.Combine(AppContext.BaseDirectory, "TestData", "dates_binary.sas7bdat");

    /// <summary>
    /// Path to a real .sas7bdat file to test against, read from the SAS_TEST_FILE_PATH
    /// variable in a local .env file. Null/empty when not configured.
    /// </summary>
    public static string LiveFilePath => Environment.GetEnvironmentVariable("SAS_TEST_FILE_PATH");
}
