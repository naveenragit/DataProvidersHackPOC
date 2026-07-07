using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Authentication;

namespace FinancialServices.Api.Connectors.Mcp;

/// <summary>
/// A local, <b>git-ignored</b> file implementation of the MCP SDK's <see cref="ITokenCache"/> for
/// <b>local dev only</b> (planning §D4). The one-time interactive login writes the OAuth token here and
/// the runtime reads + refreshes from it; because the SDK calls <see cref="StoreTokensAsync"/> on every
/// refresh, a <b>rotated</b> refresh token is persisted automatically (STK-03 write-back). Secrets are
/// never logged (P6) — only the path and non-sensitive metadata (has-refresh-token, expiry seconds).
/// <para>
/// <b>Key Vault seam:</b> this is deliberately just an <see cref="ITokenCache"/>. A production,
/// multi-replica deployment swaps in a Key-Vault-backed implementation (ETag-guarded write-back for the
/// rotation race, STK-03); nothing else changes because the SDK depends only on the interface.
/// </para>
/// </summary>
public sealed class FileTokenCache : ITokenCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly string _providerLabel;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileTokenCache(string path, string providerLabel, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerLabel);
        _path = path;
        _providerLabel = providerLabel;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>The default per-provider token file under a git-ignored <c>.prism/tokens</c> directory.</summary>
    public static string DefaultPathFor(ProviderMcpKey provider, string? baseDirectory = null)
    {
        var root = baseDirectory ?? AppContext.BaseDirectory;
        return Path.Combine(root, ".prism", "tokens", $"{provider.ToString().ToLowerInvariant()}.json");
    }

    public async ValueTask<TokenContainer?> GetTokensAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_path, cancellationToken).ConfigureAwait(false);
            var persisted = JsonSerializer.Deserialize<PersistedToken>(json, SerializerOptions);
            if (persisted is null)
            {
                _logger.LogWarning("{Provider} token cache at the configured path was empty; re-login required.", _providerLabel);
                return null;
            }

            return persisted.ToContainer();
        }
        catch (JsonException)
        {
            // A corrupt/tampered cache degrades to "no token" (→ interactive re-login overwrites it),
            // logged loud but WITHOUT contents (P6). Never rethrow raw JSON that could carry a fragment.
            _logger.LogWarning("{Provider} token cache could not be parsed; treating as empty (re-login required).", _providerLabel);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask StoreTokensAsync(TokenContainer tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(PersistedToken.From(tokens), SerializerOptions);

            // Atomic replace: write a temp sibling then move over the target so a crash mid-write never
            // leaves a half-written token file.
            var temp = _path + ".tmp";
            await File.WriteAllTextAsync(temp, json, cancellationToken).ConfigureAwait(false);
            File.Move(temp, _path, overwrite: true);

            _logger.LogInformation(
                "{Provider} token cache updated (hasRefreshToken={HasRefresh}, expiresInSeconds={ExpiresIn}).",
                _providerLabel,
                !string.IsNullOrEmpty(tokens.RefreshToken),
                tokens.ExpiresIn);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Prism-owned on-disk shape (P6 — we control exactly what is written, independent of the SDK's
    // serialization attributes). Secrets live here in cleartext by necessity for local dev; the file is
    // git-ignored and this store is dev-only (Key Vault is the production seam).
    private sealed record PersistedToken(
        string? TokenType,
        string? AccessToken,
        string? RefreshToken,
        int? ExpiresIn,
        string? Scope,
        DateTimeOffset ObtainedAt)
    {
        public static PersistedToken From(TokenContainer c) =>
            new(c.TokenType, c.AccessToken, c.RefreshToken, c.ExpiresIn, c.Scope, c.ObtainedAt);

        public TokenContainer ToContainer() => new()
        {
            TokenType = TokenType!,
            AccessToken = AccessToken!,
            RefreshToken = RefreshToken!,
            ExpiresIn = ExpiresIn,
            Scope = Scope!,
            ObtainedAt = ObtainedAt,
        };
    }
}
