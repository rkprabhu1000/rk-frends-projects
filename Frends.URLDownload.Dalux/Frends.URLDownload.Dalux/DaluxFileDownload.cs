using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Frends.URLDownload.Dalux.Definitions;
using Frends.URLDownload.Dalux.Helpers;
using Microsoft.Playwright;

namespace Frends.URLDownload.Dalux;

/// <summary>
/// Task class for Dalux file download operations.
/// </summary>
public static class URLDownload
{
    /// <summary>
    /// Logs into the Dalux platform via headless browser automation, captures the
    /// authentication token from the Authorize API response, then downloads the
    /// specified file using that token as a session cookie.
    ///
    /// Flow: navigate login page → fill email + Tab → fill password → click submit →
    /// intercept Authorize response → HTTP GET file with token cookie → return bytes.
    ///
    /// [Documentation](https://tasks.frends.com/tasks/CHANNEL/Frends.URLDownload.Dalux)
    /// </summary>
    /// <param name="input">
    /// Login credentials (Email, Password), the Dalux login page URL with redirect
    /// (CallingUrl), and the direct file download URL (FileUrl).
    /// </param>
    /// <param name="options">
    /// Headless mode flag, per-operation timeout, automatic Firefox installation,
    /// and error-handling behaviour (ThrowErrorOnFailure, ErrorMessageOnFailure).
    /// </param>
    /// <param name="cancellationToken">A cancellation token provided by the Frends Platform.</param>
    /// <returns>
    /// object {
    ///   bool Success,
    ///   string FileName,
    ///   byte[] Content,
    ///   object Error { string Message, Exception AdditionalInfo }
    /// }
    /// </returns>
    public static async Task<Result> Dalux(
        [PropertyTab] Input input,
        [PropertyTab] Options options,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(input.CallingUrl))
                throw new ArgumentException("CallingUrl cannot be empty.", nameof(input));
            if (string.IsNullOrWhiteSpace(input.FileUrl))
                throw new ArgumentException("FileUrl cannot be empty.", nameof(input));
            if (string.IsNullOrWhiteSpace(input.Email))
                throw new ArgumentException("Email cannot be empty.", nameof(input));
            if (string.IsNullOrWhiteSpace(input.Password))
                throw new ArgumentException("Password cannot be empty.", nameof(input));

            if (options.InstallBrowserIfMissing)
                BrowserInstaller.EnsureChromium();

            using var playwright = await Playwright.CreateAsync();

            // Firefox is used instead of Chromium. Chromium's renderer crashes during
            // Angular's same-URL re-navigation in this container regardless of flag
            // combinations. Firefox uses FIFO-based IPC with no zygote process model
            // and handles container namespace restrictions without special launch args.
            await using var browser = await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = options.Headless,
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36",
            });

            var page = await context.NewPageAsync();

            // Step 1: Navigate to the login page.
            // GotoAsync waits for DOMContentLoaded, then we separately wait for
            // NetworkIdle. Splitting the two waits lets us survive Angular's
            // same-URL re-navigation that fires between DOMContentLoaded and Load.
            await page.GotoAsync(input.CallingUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = options.TimeoutMs,
            });

            try
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                {
                    Timeout = options.TimeoutMs,
                });
            }
            catch (PlaywrightException)
            {
                // NetworkIdle can time out on pages with persistent polling — not fatal
                // as long as the login form rendered.
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Step 2: Fill email and press Tab (triggers any email-domain lookup).
            const string emailSelector = "input[type='email'], input[name='email'], input[placeholder*='email' i], input[placeholder*='mail' i]";
            await page.WaitForSelectorAsync(emailSelector, new PageWaitForSelectorOptions { Timeout = options.TimeoutMs });
            await page.FillAsync(emailSelector, input.Email);
            await page.Keyboard.PressAsync("Tab");
            await page.WaitForTimeoutAsync(2000);

            cancellationToken.ThrowIfCancellationRequested();

            // Step 3: Fill password, then arm the Authorize response intercept BEFORE clicking submit.
            await page.WaitForSelectorAsync("input[type='password']", new PageWaitForSelectorOptions { Timeout = options.TimeoutMs });
            await page.FillAsync("input[type='password']", input.Password);

            var authorizeTask = page.WaitForResponseAsync(
                r => r.Url.Contains("Authorize"),
                new PageWaitForResponseOptions { Timeout = options.TimeoutMs });

            const string submitSelector = "button[type='submit'], button:has-text('Login'), button:has-text('Sign in'), button:has-text('Log in')";
            await page.ClickAsync(submitSelector, new PageClickOptions { Timeout = options.TimeoutMs });

            // Step 4: Parse the access token from the Authorize API response.
            var authorizeResponse = await authorizeTask;
            var json = await authorizeResponse.JsonAsync();
            var accessToken = ExtractAccessToken(json);

            if (string.IsNullOrEmpty(accessToken))
                throw new InvalidOperationException(
                    "Failed to capture access token from Dalux Authorize response. " +
                    "Verify the credentials and that the CallingUrl triggers an Authorize API call during login.");

            cancellationToken.ThrowIfCancellationRequested();

            // Step 5: Download the file using the token as a session cookie.
            var downloadResponse = await context.APIRequest.GetAsync(
                input.FileUrl,
                new APIRequestContextOptions
                {
                    Headers = new Dictionary<string, string>
                    {
                        ["Cookie"] = $"daluxIdAuthForDownloading={accessToken}; daluxIdAuthForWOPI={accessToken}",
                    },
                    Timeout = options.TimeoutMs,
                });

            if (!downloadResponse.Ok)
                throw new InvalidOperationException(
                    $"File download failed with HTTP {downloadResponse.Status} from {input.FileUrl}.");

            var contentDisposition = downloadResponse.Headers.GetValueOrDefault("content-disposition", string.Empty);
            var fileName = ContentDispositionHelper.ExtractFileName(contentDisposition);
            var content = await downloadResponse.BodyAsync();

            return new Result
            {
                Success = true,
                FileName = fileName,
                Content = content,
            };
        }
        catch (Exception ex)
        {
            if (options.ThrowErrorOnFailure)
                throw;

            return new Result
            {
                Success = false,
                Error = new Error
                {
                    Message = string.IsNullOrWhiteSpace(options.ErrorMessageOnFailure)
                        ? ex.Message
                        : options.ErrorMessageOnFailure,
                    AdditionalInfo = ex,
                },
            };
        }
    }

    private static string ExtractAccessToken(JsonElement? json)
    {
        if (json is null || json.Value.ValueKind != JsonValueKind.Object)
            return null;

        try
        {
            if (json.Value.GetProperty("result").GetInt32() != 0)
                return null;

            return json.Value
                .GetProperty("value")
                .GetProperty("value")
                .GetProperty("accessToken")
                .GetString();
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
