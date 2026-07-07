using ModelContextProtocol.Authentication;

namespace FinancialServices.Api.Connectors.Mcp;

/// <summary>
/// Deterministic freshness math for a cached OAuth <see cref="TokenContainer"/> — plain C#, no I/O
/// (the SDK owns the actual HTTP refresh). Prism uses it to report/log how much life a token has and
/// to decide, with a safety <em>skew</em> margin, whether a refresh is due (mirrors the 60s skew the
/// wealthgen sample applied). Kept pure so the "refresh/skew" behaviour is unit-testable without a
/// network (P7 — the caller still passes the clock).
/// </summary>
public static class TokenFreshness
{
    /// <summary>The default clock skew applied before an access token is considered due for refresh.</summary>
    public static readonly TimeSpan DefaultSkew = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Seconds until <paramref name="token"/> expires relative to <paramref name="now"/> (negative =
    /// already expired). Returns <c>null</c> when the provider did not send an <c>expires_in</c> (the
    /// token has no known lifetime, so freshness cannot be computed — treat as "refresh to be safe").
    /// </summary>
    public static double? SecondsUntilExpiry(TokenContainer token, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (token.ExpiresIn is not { } expiresIn)
        {
            return null;
        }

        var expiresAt = token.ObtainedAt.AddSeconds(expiresIn);
        return (expiresAt - now).TotalSeconds;
    }

    /// <summary>
    /// Whether the access token should be refreshed now, applying <paramref name="skew"/> (default
    /// <see cref="DefaultSkew"/>). An unknown lifetime (no <c>expires_in</c>) or a blank access token
    /// ⇒ <c>true</c> (refresh to be safe). Never throws — a decision helper, not a gate.
    /// </summary>
    public static bool NeedsRefresh(TokenContainer token, DateTimeOffset now, TimeSpan? skew = null)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (string.IsNullOrEmpty(token.AccessToken))
        {
            return true;
        }

        var remaining = SecondsUntilExpiry(token, now);
        if (remaining is not { } seconds)
        {
            return true;
        }

        return seconds <= (skew ?? DefaultSkew).TotalSeconds;
    }
}
