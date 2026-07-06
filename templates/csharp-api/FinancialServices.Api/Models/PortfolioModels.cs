// Copy to backend/FinancialServices.Api/Models/PortfolioModels.cs
using System.ComponentModel.DataAnnotations;

namespace FinancialServices.Api.Models;

public enum AssetClass { Equity, FixedIncome, Derivative, Alternative }

/// <summary>A single holding within a portfolio.</summary>
public sealed record PortfolioPosition(
    [property: Required] string InstrumentId,          // ISIN or internal instrument ID
    AssetClass AssetClass,
    [property: Range(0, double.MaxValue)] decimal Quantity,
    decimal MarketValue,
    [property: Range(0, 1)] decimal Weight,
    DateTimeOffset LastUpdated);

/// <summary>Domain model persisted to Cosmos DB (portfolios container, pk: /clientId).</summary>
public sealed record PortfolioSummary(
    string Id,
    string ClientId,
    decimal TotalValue,
    string Currency,
    IReadOnlyList<PortfolioPosition> Positions,
    double? RiskScore,
    string? Benchmark);

// Request DTOs are always separate from domain models.
public sealed record RebalanceRequest(
    IReadOnlyDictionary<string, decimal> TargetWeights,
    [property: Required, MaxLength(500)] string Reason,
    string? ApprovedBy);
