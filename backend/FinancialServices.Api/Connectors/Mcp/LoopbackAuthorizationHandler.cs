using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using ModelContextProtocol.Authentication;

namespace FinancialServices.Api.Connectors.Mcp;

/// <summary>
/// The <b>interactive</b> OAuth redirect strategy for the one-time discovery login (planning §Phase-0):
/// it opens the system browser to the SDK-built authorization URL, listens on the allow-listed
/// <b>loopback</b> redirect for the callback, validates the returned <c>state</c> against the value the
/// SDK embedded in the authorization URL (SEC-04 — CSRF defence on top of PKCE), and returns the
/// authorization <c>code</c>. This runs only from the discovery CLI (a human at a browser); the runtime
/// uses <see cref="ProviderOAuth.HeadlessRedirect"/> instead.
/// </summary>
public static class LoopbackAuthorizationHandler
{
    /// <summary>
    /// Builds an <see cref="AuthorizationRedirectDelegate"/> that performs the interactive loopback
    /// flow. <paramref name="timeout"/> bounds how long the listener waits for the human to finish.
    /// </summary>
    public static AuthorizationRedirectDelegate CreateDelegate(ILogger logger, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var wait = timeout ?? TimeSpan.FromMinutes(5);

        return async (authorizationUri, redirectUri, cancellationToken) =>
        {
            var expectedState = QueryHelpers.ParseQuery(authorizationUri.Query).TryGetValue("state", out var s)
                ? s.ToString()
                : string.Empty;

            var prefix = $"{redirectUri.Scheme}://{redirectUri.Host}:{redirectUri.Port}/";
            using var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            logger.LogInformation("Waiting for the OAuth callback on {Prefix} …", prefix);

            OpenBrowser(authorizationUri, logger);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(wait);

            HttpListenerContext context;
            try
            {
                var contextTask = listener.GetContextAsync();
                var completed = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, timeoutCts.Token))
                    .ConfigureAwait(false);
                if (completed != contextTask)
                {
                    throw new TimeoutException("Timed out waiting for the OAuth callback.");
                }

                context = await contextTask.ConfigureAwait(false);
            }
            finally
            {
                // listener disposed by the using; nothing sensitive to scrub here.
            }

            var query = context.Request.Url?.Query ?? string.Empty;
            var success = TryExtractCode(expectedState, query, out var code, out var error);

            await WriteBrowserResponseAsync(context, success, error).ConfigureAwait(false);

            if (!success)
            {
                throw new InvalidOperationException($"OAuth callback rejected: {error}");
            }

            logger.LogInformation("OAuth callback received and validated (state matched).");
            return code!;
        };
    }

    /// <summary>
    /// Pure parse + validate of an OAuth redirect query (unit-testable, no I/O). Returns <c>true</c> and
    /// sets <paramref name="code"/> only when there is no <c>error</c> parameter, the <c>state</c>
    /// exactly matches <paramref name="expectedState"/> (SEC-04), and a non-empty <c>code</c> is present.
    /// </summary>
    public static bool TryExtractCode(string expectedState, string redirectQuery, out string? code, out string? error)
    {
        code = null;
        error = null;
        var parsed = QueryHelpers.ParseQuery(redirectQuery);

        if (parsed.TryGetValue("error", out StringValues errValues) && !StringValues.IsNullOrEmpty(errValues))
        {
            error = parsed.TryGetValue("error_description", out var desc) && !StringValues.IsNullOrEmpty(desc)
                ? $"{errValues}: {desc}"
                : errValues.ToString();
            return false;
        }

        var returnedState = parsed.TryGetValue("state", out var st) ? st.ToString() : string.Empty;
        if (!string.Equals(returnedState, expectedState, StringComparison.Ordinal))
        {
            error = "state mismatch (possible CSRF)";
            return false;
        }

        if (!parsed.TryGetValue("code", out var codeValues) || StringValues.IsNullOrEmpty(codeValues))
        {
            error = "authorization code missing";
            return false;
        }

        code = codeValues.ToString();
        return true;
    }

    private static void OpenBrowser(Uri url, ILogger logger)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // Not fatal: print the URL so the user can paste it manually. Never rethrow the raw error.
            logger.LogWarning("Could not launch a browser automatically ({Reason}). Open this URL to sign in:", ex.Message);
            logger.LogInformation("{AuthorizationUrl}", url);
        }
    }

    private static async Task WriteBrowserResponseAsync(HttpListenerContext context, bool success, string? error)
    {
        var body = success
            ? "<html><body><h3>Sign-in complete.</h3><p>You can close this tab and return to the terminal.</p></body></html>"
            : $"<html><body><h3>Sign-in failed.</h3><p>{WebUtility.HtmlEncode(error)}</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Response.ContentType = "text/html";
        context.Response.StatusCode = success ? 200 : 400;
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }
}
