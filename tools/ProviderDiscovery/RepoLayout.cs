namespace Prism.ProviderDiscovery;

/// <summary>
/// Resolves the repository root so the token cache (git-ignored <c>.prism/tokens</c>) and the findings
/// note (<c>.copilot-tracking/discovery</c>) land in stable, expected locations regardless of whether
/// the CLI runs via <c>dotnet run</c> (cwd = repo root) or as the built exe from its <c>bin</c> folder.
/// </summary>
public static class RepoLayout
{
    /// <summary>
    /// Walks up from the current directory (then the assembly's base directory) looking for a repo
    /// marker (<c>.git</c> or <c>FinancialServices.slnx</c>). Falls back to the current directory.
    /// </summary>
    public static string ResolveRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                    File.Exists(Path.Combine(dir.FullName, "backend", "FinancialServices.slnx")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
