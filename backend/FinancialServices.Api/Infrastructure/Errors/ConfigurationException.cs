namespace FinancialServices.Api.Infrastructure.Errors;

/// <summary>
/// A required configuration setting is missing or invalid (architecturalPlan/03 taxonomy). Prism
/// binds options loosely so <c>/api/health</c> and synthetic-only runs boot without every secret,
/// then fails <b>loud at the point of first real use</b> (P1) naming the missing setting — never a
/// placeholder/fake fallback. BCL-only; middleware surfaces it as a configuration error.
/// </summary>
public sealed class ConfigurationException : Exception
{
    /// <summary>Stable, client-switchable error code for the standard error response shape.</summary>
    public const string ErrorCode = "CONFIGURATION_INVALID";

    public ConfigurationException(string setting, string message, Exception? inner = null)
        : base(message, inner)
    {
        Setting = setting;
    }

    /// <summary>The configuration key that is missing/invalid (e.g. <c>"Prism:FredApiKey"</c>).</summary>
    public string Setting { get; }

    /// <summary>Stable UPPER_SNAKE code surfaced to clients.</summary>
    public string Code => ErrorCode;
}
