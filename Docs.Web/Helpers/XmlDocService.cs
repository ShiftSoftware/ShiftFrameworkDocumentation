using Microsoft.AspNetCore.Components;
using System.Reflection;
using System.Xml.Linq;

namespace Docs.Web.Services;

/// <summary>
/// Loads XML documentation files for ShiftBlazor / ShiftEntity / ShiftIdentity assemblies
/// from wwwroot/xmldocs/ at runtime, parses the &lt;member&gt; entries, and resolves
/// &lt;summary&gt; text for a given <see cref="MemberInfo"/>.
///
/// Files are fetched lazily on first request per assembly and cached in memory for the
/// lifetime of the WASM session.
/// </summary>
public class XmlDocService
{
    private readonly HttpClient _http;
    private readonly NavigationManager _nav;
    private readonly Dictionary<string, Dictionary<string, string>> _cache = new();
    private readonly Dictionary<string, Task> _inflight = new();

    public XmlDocService(HttpClient http, NavigationManager nav)
    {
        _http = http;
        _nav = nav;
    }

    /// <summary>
    /// Returns the &lt;summary&gt; text for the given member, or null if no XML doc is
    /// available. Triggers a lazy fetch of the assembly's XML doc file on first call.
    /// </summary>
    public async Task<string?> GetSummaryAsync(MemberInfo member)
    {
        var assembly = (member as Type)?.Assembly ?? member.DeclaringType?.Assembly;
        if (assembly is null) return null;

        var asmName = assembly.GetName().Name;
        if (string.IsNullOrEmpty(asmName)) return null;

        await EnsureLoadedAsync(asmName);

        if (!_cache.TryGetValue(asmName, out var members)) return null;

        var key = BuildMemberKey(member);
        return key is not null && members.TryGetValue(key, out var summary) ? summary : null;
    }

    /// <summary>
    /// Synchronous lookup against the in-memory cache. Returns null if the assembly's
    /// XML doc file hasn't been loaded yet — call <see cref="GetSummaryAsync"/> first
    /// to prime the cache.
    /// </summary>
    public string? TryGetSummary(MemberInfo member)
    {
        var asmName = (member as Type)?.Assembly.GetName().Name
                      ?? member.DeclaringType?.Assembly.GetName().Name;
        if (asmName is null) return null;
        if (!_cache.TryGetValue(asmName, out var members)) return null;
        var key = BuildMemberKey(member);
        return key is not null && members.TryGetValue(key, out var s) ? s : null;
    }

    private async Task EnsureLoadedAsync(string assemblyName)
    {
        if (_cache.ContainsKey(assemblyName)) return;

        if (_inflight.TryGetValue(assemblyName, out var existing))
        {
            await existing;
            return;
        }

        var task = LoadAsync(assemblyName);
        _inflight[assemblyName] = task;
        try { await task; }
        finally { _inflight.Remove(assemblyName); }
    }

    private async Task LoadAsync(string assemblyName)
    {
        try
        {
            var url = new Uri(_nav.BaseUri + "xmldocs/" + assemblyName + ".xml");
            var xml = await _http.GetStringAsync(url);
            _cache[assemblyName] = Parse(xml);
        }
        catch
        {
            // No XML doc shipped for this assembly — cache an empty map so we don't retry.
            _cache[assemblyName] = new Dictionary<string, string>();
        }
    }

    private static Dictionary<string, string> Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        var members = doc.Root?.Element("members")?.Elements("member") ?? Enumerable.Empty<XElement>();
        foreach (var m in members)
        {
            var name = m.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(name)) continue;

            var summary = m.Element("summary")?.Value;
            if (string.IsNullOrWhiteSpace(summary)) continue;

            result[name] = NormalizeWhitespace(summary);
        }
        return result;
    }

    private static string NormalizeWhitespace(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var trimmed = lines.Select(l => l.Trim()).Where(l => l.Length > 0);
        return string.Join(' ', trimmed);
    }

    /// <summary>
    /// Builds the XML doc member key for a property or type, matching the format used
    /// by csc when generating the XML doc file (e.g. "P:Namespace.Type.PropertyName").
    /// </summary>
    private static string? BuildMemberKey(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo p when p.DeclaringType is not null
                => $"P:{p.DeclaringType.FullName}.{p.Name}",
            Type t => $"T:{t.FullName}",
            _ => null,
        };
    }
}
