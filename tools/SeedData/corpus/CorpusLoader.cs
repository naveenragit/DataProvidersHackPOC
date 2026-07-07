using System.Text.Json;
using Prism.SeedData.Model;

namespace Prism.SeedData.Corpus;

/// <summary>
/// Loads the labeled-synthetic corpus (one JSON file per doc) from disk. Fails loud (P1) on a missing
/// directory, an empty corpus, malformed JSON, or a null document — never silently skips.
/// </summary>
public static class CorpusLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The corpus directory shipped next to the assembly (see SeedData.csproj Content copy).</summary>
    public static string ResolveDefaultCorpusRoot() => Path.Combine(AppContext.BaseDirectory, "corpus");

    /// <summary>Load and parse every <c>*.json</c> under <paramref name="corpusRoot"/> (recursively).</summary>
    public static IReadOnlyList<CorpusDoc> LoadFromDirectory(string corpusRoot)
    {
        if (!Directory.Exists(corpusRoot))
        {
            throw new DirectoryNotFoundException($"Corpus directory not found: {corpusRoot}");
        }

        var files = Directory
            .GetFiles(corpusRoot, "*.json", SearchOption.AllDirectories)
            .OrderBy(static f => f, StringComparer.Ordinal)
            .ToArray();

        if (files.Length == 0)
        {
            throw new InvalidOperationException($"No corpus JSON files found under {corpusRoot}.");
        }

        var docs = new List<CorpusDoc>(files.Length);
        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            CorpusDoc? doc;
            try
            {
                doc = JsonSerializer.Deserialize<CorpusDoc>(json, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Malformed corpus document '{file}': {ex.Message}", ex);
            }

            if (doc is null)
            {
                throw new InvalidOperationException($"Corpus document '{file}' deserialized to null.");
            }

            docs.Add(doc);
        }

        return docs;
    }
}
