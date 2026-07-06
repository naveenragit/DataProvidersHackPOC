// Copy to backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs
using Azure.Identity;
using FinancialServices.Api.Agents;
using FinancialServices.Api.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Register Azure SDK clients as singletons using DefaultAzureCredential.</summary>
    public static IServiceCollection AddAzureClients(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<AzureOptions>>().Value;
            return new CosmosClient(
                opts.CosmosEndpoint,
                new DefaultAzureCredential(),
                new CosmosClientOptions
                {
                    SerializerOptions = new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                    },
                });
        });
        return services;
    }

    /// <summary>Register domain services and Microsoft Agent Framework agents.</summary>
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<PortfolioAnalysisAgent>();
        return services;
    }

    /// <summary>Wire OpenTelemetry to Azure Monitor (APPLICATIONINSIGHTS_CONNECTION_STRING).</summary>
    public static IServiceCollection AddAppTelemetry(this IServiceCollection services, IConfiguration config)
    {
        var hasConnectionString = !string.IsNullOrWhiteSpace(
            config["APPLICATIONINSIGHTS_CONNECTION_STRING"]);

        var otel = services.AddOpenTelemetry();
        if (hasConnectionString)
        {
            otel.UseAzureMonitor();
        }

        otel.WithTracing(t => t
            .AddSource("FinancialServices.Agents")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation());

        return services;
    }
}
