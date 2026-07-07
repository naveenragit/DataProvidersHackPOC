using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinancialServices.Api.Infrastructure;

/// <summary>
/// The one canonical <see cref="JsonSerializerOptions"/> for Prism's wire + persistence format:
/// <b>camelCase</b> properties (matches <c>frontend/src/types/prism.ts</c> and the Cosmos <c>id</c> /
/// <c>issuerId</c> requirement) with C# enums serialized as their member <b>names</b> (e.g.
/// <c>"Msci"</c>) via <see cref="JsonStringEnumConverter"/> — the cross-package DTO contract
/// (architecturalPlan/09). Shared by MVC, the exception handler, and the Cosmos serializer so the
/// shape never drifts between the three.
/// </summary>
public static class PrismJson
{
    /// <summary>A ready-to-use singleton (handler + Cosmos). Web defaults + enum-as-string.</summary>
    public static JsonSerializerOptions Options { get; } = Create();

    /// <summary>Builds a fresh options instance (MVC needs its own mutable copy to configure).</summary>
    public static JsonSerializerOptions Create() =>
        new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
        };
}
