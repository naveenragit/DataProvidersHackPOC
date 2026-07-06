using System.Diagnostics;

namespace FinancialServices.Api.Infrastructure.Telemetry;

/// <summary>
/// Shared <see cref="ActivitySource"/> for connector spans (architecturalPlan/07). Zero-package:
/// <see cref="ActivitySource"/>/<see cref="Activity"/> ship in the BCL. Pkg 07 adds
/// <c>AddSource("FinancialServices.Connectors")</c> + an exporter and these spans light up.
/// Tag only safe ids (cik, asOf, seriesId, latestFilingDate) — never financials or PII.
/// </summary>
public static class ConnectorTelemetry
{
    public static readonly ActivitySource Source = new("FinancialServices.Connectors");
}
