# 05 — Configuration & Secrets

One way to configure the app: the options pattern, bound once, validated at startup.

---

## Options types

- `AzureOptions` (section `Azure`) — org-standard Azure endpoints (Foundry, OpenAI, Cosmos, Search,
  Content Safety). Already in the accelerator.
- `PrismOptions` (section `Prism`) — Prism-specific settings:

```csharp
public sealed class PrismOptions
{
    [Required] public required string FredApiKey { get; init; }
    [Required] public required string SecUserAgent { get; init; }   // SEC requires a descriptive UA
    public ModelOptions Models { get; init; } = new();
    public sealed class ModelOptions
    {
        public string Orchestrator { get; init; } = "gpt-5";
        public string Provider     { get; init; } = "gpt-4.1-mini";
        public string Fundamentals { get; init; } = "gpt-4.1-mini";
        public string RedFlag      { get; init; } = "gpt-5-mini";
    }
}
```

Register with validation:
```csharp
builder.Services.AddOptions<PrismOptions>().Bind(cfg.GetSection("Prism"))
    .ValidateDataAnnotations().ValidateOnStart();
```

## Rules

- **Never** call `Environment.GetEnvironmentVariable` or read `IConfiguration` in business logic. Bind
  once → inject `IOptions<T>`.
- Missing required setting ⇒ app fails at boot naming the setting (P1). No placeholder/fake fallback.
- Env binding uses the double-underscore convention: `Azure__CosmosEndpoint`, `Prism__FredApiKey`,
  `Prism__Models__Orchestrator`.

## Secrets

| Environment | Source |
|---|---|
| Local dev | `.env` (git-ignored) or **.NET user-secrets**; `az login` for Azure creds |
| Azure (prod) | Managed identity (`DefaultAzureCredential`) + Container Apps secrets / **Key Vault** |

- **Never** commit secrets. `.gitignore` covers `.env`, `.env.*` (except `.env.example`), `bin/`,
  `obj/`, local secret files.
- **Never** bake secrets into container images or ship them to the frontend. The browser holds no
  Azure credentials — the Node sidecar and API do (via managed identity in Azure).
- Keep `.env.example` current: every new setting is documented there with a placeholder.

## Node sidecar config

`copilot-runtime` reads `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT`,
`AZURE_OPENAI_API_VERSION`, `PRISM_AGUI_URL`, `API_BASE_URL` from the environment — same no-hardcode,
no-fake rules apply.

## Model selection

Deployment names come from `PrismOptions.Models` (surfaced in the Settings page). Agents receive the
name via options — never a literal string in the agent file.
