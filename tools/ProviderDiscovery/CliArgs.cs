using FinancialServices.Api.Connectors.Mcp;

namespace Prism.ProviderDiscovery;

/// <summary>Parsed <c>ProviderDiscovery</c> command-line arguments (see <see cref="Usage"/>).</summary>
public sealed class CliArgs
{
    public ProviderMcpKey Provider { get; private init; }

    public string? CallTool { get; private init; }

    public string? CallArgsJson { get; private init; }

    public int TimeoutMinutes { get; private init; } = 5;

    public string? OutputDirectory { get; private init; }

    public static string Usage =>
        """
        Usage: ProviderDiscovery --provider morningstar|moodys [--call <tool>] [--args <json>]
                                 [--timeout <minutes>] [--out <dir>]
          --provider   (required) which hosted MCP server to discover
          --call       (optional) also run one sample tools/call
          --args       (optional) JSON object of arguments for --call
          --timeout    (optional) login wait in minutes (default 5)
          --out        (optional) findings-note directory
        """;

    public static bool TryParse(string[] args, out CliArgs cli, out string? error)
    {
        cli = new CliArgs();
        error = null;

        string? provider = null;
        string? callTool = null;
        string? callArgs = null;
        string? outDir = null;
        var timeout = 5;

        for (var i = 0; i < args.Length; i++)
        {
            var flag = args[i];
            switch (flag)
            {
                case "--provider":
                    if (!TryTakeValue(args, ref i, out provider))
                    {
                        error = "--provider requires a value.";
                        return false;
                    }

                    break;
                case "--call":
                    if (!TryTakeValue(args, ref i, out callTool))
                    {
                        error = "--call requires a tool name.";
                        return false;
                    }

                    break;
                case "--args":
                    if (!TryTakeValue(args, ref i, out callArgs))
                    {
                        error = "--args requires a JSON value.";
                        return false;
                    }

                    break;
                case "--timeout":
                    if (!TryTakeValue(args, ref i, out var timeoutText) ||
                        !int.TryParse(timeoutText, out timeout) || timeout <= 0)
                    {
                        error = "--timeout requires a positive integer (minutes).";
                        return false;
                    }

                    break;
                case "--out":
                    if (!TryTakeValue(args, ref i, out outDir))
                    {
                        error = "--out requires a directory.";
                        return false;
                    }

                    break;
                case "-h":
                case "--help":
                    error = "Help requested.";
                    return false;
                default:
                    error = $"Unknown argument '{flag}'.";
                    return false;
            }
        }

        if (!TryParseProvider(provider, out var providerKey))
        {
            error = "--provider must be 'morningstar' or 'moodys'.";
            return false;
        }

        cli = new CliArgs
        {
            Provider = providerKey,
            CallTool = callTool,
            CallArgsJson = callArgs,
            TimeoutMinutes = timeout,
            OutputDirectory = outDir,
        };
        return true;
    }

    private static bool TryTakeValue(string[] args, ref int i, out string? value)
    {
        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            value = args[++i];
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryParseProvider(string? text, out ProviderMcpKey provider)
    {
        provider = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        switch (text.Trim().ToLowerInvariant())
        {
            case "morningstar":
            case "morningstardbrs":
            case "dbrs":
                provider = ProviderMcpKey.Morningstar;
                return true;
            case "moodys":
            case "moody":
            case "moody's":
                provider = ProviderMcpKey.Moodys;
                return true;
            default:
                return false;
        }
    }
}
