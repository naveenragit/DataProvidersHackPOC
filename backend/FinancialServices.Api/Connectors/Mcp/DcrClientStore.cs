using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Authentication;

namespace FinancialServices.Api.Connectors.Mcp;

/// <summary>
/// A local, <b>git-ignored</b> store for the OAuth client credentials the MCP SDK issues via RFC 7591
/// <b>dynamic client registration</b> (DCR). Morningstar's and Moody's hosted MCP servers advertise a
/// <c>registration_endpoint</c> and expect clients to register on the fly (there is no long-lived
/// pre-registered <c>client_id</c>); the SDK performs that registration when <see cref="ClientOAuthOptions.ClientId"/>
/// is left blank. We must persist the issued <c>client_id</c> because the refresh token the SDK caches is
/// bound to it — on a later run (or headless refresh) the SDK has to present the <b>same</b> client, or
/// the refresh fails and the SDK re-registers (orphaning the cached refresh token). Sits beside the
/// <see cref="FileTokenCache"/> under <c>.prism/tokens</c>. Secrets are never logged (P6).
/// <para>
/// <b>Key Vault seam:</b> local-dev only. A production deployment persists the registered client the same
/// way it persists tokens (Key Vault); nothing else changes.
/// </para>
/// </summary>
public sealed class DcrClientStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly string _providerLabel;
    private readonly ILogger _logger;

    public DcrClientStore(string path, string providerLabel, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerLabel);
        _path = path;
        _providerLabel = providerLabel;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>The default per-provider client file under the git-ignored <c>.prism/tokens</c> directory.</summary>
    public static string DefaultPathFor(ProviderMcpKey provider, string? baseDirectory = null)
    {
        var root = baseDirectory ?? AppContext.BaseDirectory;
        return Path.Combine(root, ".prism", "tokens", $"{provider.ToString().ToLowerInvariant()}.client.json");
    }

    /// <summary>Loads a previously-registered client, or <see langword="null"/> if none / unreadable.</summary>
    public RegisteredClient? Load()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var client = JsonSerializer.Deserialize<RegisteredClient>(json, SerializerOptions);
            if (client is null || string.IsNullOrWhiteSpace(client.ClientId))
            {
                _logger.LogWarning("{Provider} registered-client file was empty; will register a new client.", _providerLabel);
                return null;
            }

            _logger.LogInformation("{Provider} reusing a previously registered OAuth client (DCR).", _providerLabel);
            return client;
        }
        catch (JsonException)
        {
            // Corrupt/tampered file degrades to "not registered" (→ a fresh DCR overwrites it), logged
            // loud but WITHOUT contents (P6).
            _logger.LogWarning("{Provider} registered-client file could not be parsed; will register a new client.", _providerLabel);
            return null;
        }
    }

    /// <summary>
    /// A <see cref="DynamicClientRegistrationOptions.ResponseDelegate"/> that atomically persists the
    /// client the SDK just registered, so subsequent runs reuse it instead of re-registering.
    /// </summary>
    public async Task OnRegisteredAsync(DynamicClientRegistrationResponse response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var client = new RegisteredClient(response.ClientId, response.ClientSecret, response.TokenEndpointAuthMethod);
        var json = JsonSerializer.Serialize(client, SerializerOptions);

        // Atomic replace: write a temp sibling then move over the target (never leave a half-written file).
        var temp = _path + ".tmp";
        await File.WriteAllTextAsync(temp, json, cancellationToken).ConfigureAwait(false);
        File.Move(temp, _path, overwrite: true);

        _logger.LogInformation(
            "{Provider} dynamically registered a new OAuth client and persisted it for reuse (hasSecret={HasSecret}).",
            _providerLabel,
            !string.IsNullOrEmpty(response.ClientSecret));
    }
}

/// <summary>An OAuth client issued by RFC 7591 dynamic client registration, persisted for reuse.</summary>
public sealed record RegisteredClient(string ClientId, string? ClientSecret, string? TokenEndpointAuthMethod = null);
