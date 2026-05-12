using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Playwright;

namespace Frends.URLDownload.Dalux.Helpers;

internal static class BrowserInstaller
{
    internal static void EnsureChromium()
    {
        // Frends flattens all lib/net8.0/** files into the process directory root,
        // so the .playwright/ subdirectory structure is lost at deploy time.
        // We ship a single driver.zip which survives the flattening, then extract
        // it here at first run to recreate .playwright/ next to the assembly.
        var assemblyDir = Path.GetDirectoryName(typeof(BrowserInstaller).Assembly.Location);
        if (string.IsNullOrEmpty(assemblyDir))
        {
            assemblyDir = AppContext.BaseDirectory;
        }

        var driverZipPath = Path.Combine(assemblyDir, "driver.zip");
        var playwrightDir = Path.Combine(assemblyDir, ".playwright");

        if (File.Exists(driverZipPath) && !Directory.Exists(playwrightDir))
        {
            ZipFile.ExtractToDirectory(driverZipPath, assemblyDir);

            // On Linux, the extracted node binary needs the execute permission bit set.
            var nodeLinux = Path.Combine(playwrightDir, "node", "linux-x64", "node");
            if (File.Exists(nodeLinux) && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(
                    nodeLinux,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }

        // On Linux, minimal container images are often missing the system-level shared
        // libraries that chrome-headless-shell needs (libatk-1.0.so.0, libgbm.so.1, etc.).
        // We ship linux-libs.zip with the key .so files and extract it on first run, then
        // prepend its path to LD_LIBRARY_PATH so Chromium's dynamic linker finds them.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var linuxLibsZip = Path.Combine(assemblyDir, "linux-libs.zip");
            var linuxLibsDir = Path.Combine(assemblyDir, "linux-libs");

            // Re-extract whenever the zip is newer than the directory — this ensures a
            // freshly-deployed NuGet version always wins over a stale cached extraction.
            if (File.Exists(linuxLibsZip))
            {
                var zipTime = File.GetLastWriteTimeUtc(linuxLibsZip);
                var dirTime = Directory.Exists(linuxLibsDir)
                    ? new DirectoryInfo(linuxLibsDir).LastWriteTimeUtc
                    : DateTime.MinValue;

                if (zipTime > dirTime)
                {
                    if (Directory.Exists(linuxLibsDir))
                        Directory.Delete(linuxLibsDir, recursive: true);
                    ZipFile.ExtractToDirectory(linuxLibsZip, linuxLibsDir);
                }
            }

            if (Directory.Exists(linuxLibsDir))
            {
                var current = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? string.Empty;
                var updated = string.IsNullOrEmpty(current)
                    ? linuxLibsDir
                    : $"{linuxLibsDir}:{current}";
                Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", updated);
            }
        }

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")))
        {
            Environment.SetEnvironmentVariable(
                "PLAYWRIGHT_BROWSERS_PATH",
                Path.Combine(Path.GetTempPath(), "ms-playwright"));
        }

        // Install Firefox instead of Chromium.
        // Chromium's multi-process renderer architecture (even with --no-zygote and
        // --single-process) crashes in the Frends Linux container during Angular's
        // same-URL re-navigation. Firefox uses a different IPC model (FIFO, not shared
        // memory) with no zygote dependency, and handles container namespace restrictions
        // far better.
        var exitCode = Program.Main(new[] { "install", "firefox" });
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"'playwright install firefox' exited with code {exitCode}. " +
                $"driverZip=[{File.Exists(driverZipPath)}] " +
                $"playwrightDir=[{Directory.Exists(playwrightDir)}] " +
                "Ensure the agent has internet access and write permission to the browser cache directory.");
        }
    }
}
