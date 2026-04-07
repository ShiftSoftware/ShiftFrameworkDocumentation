using Microsoft.AspNetCore.Components;
using System.Text.Json;

namespace Docs.Web.Services;

public record SearchEntry(
    string Url,
    string Title,
    string? Section,
    string Status,
    string? Description,
    string? Keywords);

public record SearchHit(SearchEntry Entry, int Score);

/// <summary>
/// Loads <c>wwwroot/search-index.json</c> (produced by Docs.SnippetGen at build time)
/// and serves simple substring search across page title / section / description / keywords.
///
/// Scoring is intentionally crude — title hits weigh most, then keywords, then description.
/// Good enough for a few hundred pages; revisit if the index grows past that.
/// </summary>
public class SearchService
{
    private readonly HttpClient _http;
    private readonly NavigationManager _nav;
    private List<SearchEntry>? _index;
    private Task? _loadTask;

    public SearchService(HttpClient http, NavigationManager nav)
    {
        _http = http;
        _nav = nav;
    }

    public async Task EnsureLoadedAsync()
    {
        if (_index is not null) return;
        _loadTask ??= LoadAsync();
        await _loadTask;
    }

    private async Task LoadAsync()
    {
        try
        {
            var url = new Uri(_nav.BaseUri + "search-index.json");
            var json = await _http.GetStringAsync(url);
            _index = JsonSerializer.Deserialize<List<SearchEntry>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? new();
        }
        catch
        {
            _index = new();
        }
    }

    /// <summary>
    /// Returns all indexed pages grouped by their declared <c>Section</c>, in the
    /// order specified by <paramref name="sectionOrder"/>. Sections not in the
    /// order list are appended alphabetically at the end. Pages with no section
    /// are grouped under "Other".
    /// </summary>
    public async Task<IReadOnlyList<(string Section, IReadOnlyList<SearchEntry> Pages)>> GetAllGroupedBySectionAsync(
        IReadOnlyList<string>? sectionOrder = null)
    {
        await EnsureLoadedAsync();
        if (_index is null) return Array.Empty<(string, IReadOnlyList<SearchEntry>)>();

        var grouped = _index
            .GroupBy(e => string.IsNullOrWhiteSpace(e.Section) ? "Other" : e.Section!)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SearchEntry>)g.OrderBy(e => e.Title).ToList());

        var ordered = new List<(string, IReadOnlyList<SearchEntry>)>();
        if (sectionOrder is not null)
        {
            foreach (var section in sectionOrder)
            {
                if (grouped.TryGetValue(section, out var pages))
                {
                    ordered.Add((section, pages));
                    grouped.Remove(section);
                }
            }
        }
        foreach (var kv in grouped.OrderBy(k => k.Key))
            ordered.Add((kv.Key, kv.Value));

        return ordered;
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int max = 10)
    {
        await EnsureLoadedAsync();
        if (_index is null || string.IsNullOrWhiteSpace(query)) return Array.Empty<SearchHit>();

        var q = query.Trim();
        var hits = new List<SearchHit>();

        foreach (var entry in _index)
        {
            var score = Score(entry, q);
            if (score > 0) hits.Add(new SearchHit(entry, score));
        }

        return hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Entry.Title)
            .Take(max)
            .ToList();
    }

    private static int Score(SearchEntry e, string q)
    {
        int score = 0;
        if (Contains(e.Title, q)) score += e.Title.Equals(q, StringComparison.OrdinalIgnoreCase) ? 100 : 50;
        if (Contains(e.Keywords, q)) score += 30;
        if (Contains(e.Section, q)) score += 20;
        if (Contains(e.Description, q)) score += 10;
        return score;
    }

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrEmpty(haystack) &&
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
