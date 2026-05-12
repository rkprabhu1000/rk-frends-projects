using System;
using dotenv.net;

namespace Frends.URLDownload.Dalux.Tests;

public abstract class TestBase
{
    protected TestBase()
    {
        DotEnv.Load();
    }

    protected static bool HasEnvVar(string name) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name));

    protected static string Env(string name) =>
        Environment.GetEnvironmentVariable(name) ?? string.Empty;
}
