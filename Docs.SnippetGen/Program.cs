using System.Text.Json;
using System.Text.RegularExpressions;

// Docs.SnippetGen
// ----------------
// Extracts code snippets from source files into Docs.Web/wwwroot/snippets/*.txt
// so they can be displayed in documentation pages.
//
// Two extraction modes:
//
//   1. Whole-file mode (Razor component demos)
//      Every .razor file under Docs.Web/Pages/Components/** (excluding files
//      directly under Components/, plus Index.razor / IndexAPI.razor) is
//      copied verbatim to {FileNameWithoutExt}.txt. This is what
//      DocCodeViewer<T> consumes via DocFileHelper.GetDocFile(typeof(T)).
//
//   2. Named-region mode (prose snippets)
//      Any .cs or .razor file under any configured source root may contain
//      one or more named snippet regions. Each region becomes its own
//      {SnippetName}.txt file consumed by <DocSnippet Name="..." />.
//
//      Marker syntax:
//        C#:    // snippet:UpsertEndpoint
//                  ... code ...
//               // endsnippet
//
//        Razor: @* snippet:CustomerForm *@
//                  ... markup ...
//               @* endsnippet *@
//
//      Regions are de-indented (common leading whitespace stripped) so they
//      render flush-left regardless of where they sit in the source file.
//      Snippet names must be unique across the entire scan; duplicates
//      cause the build to fail loudly.

var solutionRoot = FindSolutionRoot() ?? Directory.GetCurrentDirectory();
var docsWebRoot = Path.Combine(solutionRoot, "Docs.Web");
var outRoot = Path.Combine(docsWebRoot, "wwwroot", "snippets");

// Resolve the current Shift Framework version from ShiftTemplates' global props.
// Pages declare TestedAgainst on their <DocPageMeta /> and we warn (not fail) when
// a non-Draft page has fallen behind the current version.
var currentFrameworkVersion = ResolveFrameworkVersion(solutionRoot);

Console.WriteLine("==========");
Console.WriteLine("Snippet Generator started");
Console.WriteLine($"[SnippetGen] current framework version: {currentFrameworkVersion ?? "unknown"}");

Directory.CreateDirectory(outRoot);

// --- Whole-file mode: Razor component demos ---------------------------------

var componentDemoRoot = Path.Combine(docsWebRoot, "Pages", "Components");
if (Directory.Exists(componentDemoRoot))
{
    var demoFiles = Directory.EnumerateFiles(componentDemoRoot, "*.razor", SearchOption.AllDirectories)
        .Where(full =>
        {
            var fileName = Path.GetFileName(full);
            if (fileName.Equals("Index.razor", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("IndexAPI.razor", StringComparison.OrdinalIgnoreCase))
                return false;

            // Skip files sitting directly under Pages/Components (those are landing pages, not demos)
            var fileDir = Path.GetFullPath(Path.GetDirectoryName(full)!)
                              .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var srcDir = Path.GetFullPath(componentDemoRoot)
                              .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return !string.Equals(fileDir, srcDir, StringComparison.OrdinalIgnoreCase);
        });

    foreach (var file in demoFiles)
        CopyWholeFile(file, outRoot);
}

// --- Named-region mode: scan all source roots for marked regions ------------

// Source roots scanned for named snippet regions. Add new project folders here
// as the docs grow (e.g. Docs.Functions, additional sample projects).
// Docs.Verify is the home for *anchored* commands and code that the docs reference
// instead of typing inline — see Docs.Verify/README.md.
var regionScanRoots = new[]
{
    Path.Combine(docsWebRoot, "Pages"),
    Path.Combine(solutionRoot, "Docs.API"),
    Path.Combine(solutionRoot, "Docs.Data"),
    Path.Combine(solutionRoot, "Docs.Shared"),
    Path.Combine(solutionRoot, "Docs.Verify"),
}.Where(Directory.Exists).ToArray();

var seenSnippetNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

foreach (var root in regionScanRoots)
{
    var sourceFiles = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
        .Where(f =>
        {
            // Skip build output
            if (f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) return false;
            if (f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) return false;
            var ext = Path.GetExtension(f);
            return ext.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".razor", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".sh", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".bash", StringComparison.OrdinalIgnoreCase);
        });

    foreach (var file in sourceFiles)
        ExtractRegions(file, outRoot, seenSnippetNames);
}

Console.WriteLine($"[SnippetGen] {seenSnippetNames.Count} named snippet(s) extracted");

// --- Search index: scan all .razor pages under Docs.Web/Pages for <DocPageMeta /> -------

var pagesRoot = Path.Combine(docsWebRoot, "Pages");
var indexEntries = new List<SearchEntry>();

if (Directory.Exists(pagesRoot))
{
    foreach (var razorFile in Directory.EnumerateFiles(pagesRoot, "*.razor", SearchOption.AllDirectories))
    {
        var entry = ExtractPageMeta(razorFile);
        if (entry is not null) indexEntries.Add(entry);
    }
}

var indexJson = JsonSerializer.Serialize(indexEntries, new JsonSerializerOptions
{
    WriteIndented = false,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
});
File.WriteAllText(Path.Combine(docsWebRoot, "wwwroot", "search-index.json"), indexJson);
Console.WriteLine($"[SnippetGen] search index: {indexEntries.Count} page(s)");

// --- Tier 1 verification: warn on stale TestedAgainst ----------------------

if (!string.IsNullOrEmpty(currentFrameworkVersion))
{
    int staleCount = 0;
    foreach (var entry in indexEntries)
    {
        if (entry.Status == "Draft") continue;          // Drafts aren't expected to be verified.
        if (string.IsNullOrEmpty(entry.TestedAgainst))
        {
            Console.WriteLine($"[SnippetGen] warning: {entry.Url} ({entry.Status}) has no TestedAgainst — bump it or downgrade to Draft.");
            staleCount++;
        }
        else if (!entry.TestedAgainst.Equals(currentFrameworkVersion, StringComparison.Ordinal))
        {
            Console.WriteLine($"[SnippetGen] warning: {entry.Url} TestedAgainst={entry.TestedAgainst} but current is {currentFrameworkVersion} — re-verify the page.");
            staleCount++;
        }
    }
    if (staleCount > 0)
        Console.WriteLine($"[SnippetGen] {staleCount} page(s) need re-verification against framework {currentFrameworkVersion}.");
}

Console.WriteLine("[SnippetGen] done");
return;

static void CopyWholeFile(string fullPath, string outRoot)
{
    var baseName = Path.GetFileNameWithoutExtension(fullPath);
    var outPath = Path.Combine(outRoot, $"{baseName}.txt");
    File.WriteAllText(outPath, File.ReadAllText(fullPath));
}

static void ExtractRegions(string fullPath, string outRoot, Dictionary<string, string> seen)
{
    var text = File.ReadAllText(fullPath);
    var ext = Path.GetExtension(fullPath);

    // Match the comment marker style appropriate to the file extension.
    //   C#:    // snippet:Name ... // endsnippet
    //   Razor: @* snippet:Name *@ ... @* endsnippet *@
    //   Bash:  # snippet:Name ... # endsnippet
    string pattern;
    if (ext.Equals(".cs", StringComparison.OrdinalIgnoreCase))
        pattern = @"//\s*snippet:([A-Za-z0-9_\-]+)\s*\r?\n(.*?)//\s*endsnippet";
    else if (ext.Equals(".sh", StringComparison.OrdinalIgnoreCase) || ext.Equals(".bash", StringComparison.OrdinalIgnoreCase))
        pattern = @"#\s*snippet:([A-Za-z0-9_\-]+)\s*\r?\n(.*?)#\s*endsnippet";
    else
        pattern = @"@\*\s*snippet:([A-Za-z0-9_\-]+)\s*\*@\s*\r?\n(.*?)@\*\s*endsnippet\s*\*@";

    var matches = Regex.Matches(text, pattern, RegexOptions.Singleline);
    foreach (Match m in matches)
    {
        var name = m.Groups[1].Value;
        var body = m.Groups[2].Value.TrimEnd('\r', '\n');

        if (seen.TryGetValue(name, out var firstFile))
        {
            Console.Error.WriteLine(
                $"[SnippetGen] ERROR: duplicate snippet name '{name}' in {fullPath} (first defined in {firstFile})");
            Environment.Exit(1);
        }
        seen[name] = fullPath;

        var dedented = Dedent(body);
        File.WriteAllText(Path.Combine(outRoot, $"{name}.txt"), dedented);
    }
}

static string Dedent(string text)
{
    var lines = text.Split('\n');
    var minIndent = int.MaxValue;
    foreach (var rawLine in lines)
    {
        var line = rawLine.TrimEnd('\r');
        if (string.IsNullOrWhiteSpace(line)) continue;
        var indent = 0;
        while (indent < line.Length && line[indent] == ' ') indent++;
        if (indent < minIndent) minIndent = indent;
    }
    if (minIndent == int.MaxValue || minIndent == 0) return text;

    return string.Join('\n', lines.Select(l =>
    {
        var stripped = l.TrimEnd('\r');
        return stripped.Length >= minIndent ? stripped.Substring(minIndent) : stripped;
    }));
}

static SearchEntry? ExtractPageMeta(string razorFile)
{
    var text = File.ReadAllText(razorFile);

    // Pull the route from @page "..."
    var pageMatch = Regex.Match(text, @"@page\s+""([^""]+)""");
    if (!pageMatch.Success) return null;
    var url = pageMatch.Groups[1].Value;

    // Pull the <DocPageMeta ... /> tag (allow multi-line)
    var metaMatch = Regex.Match(text, @"<DocPageMeta\b([^/>]*)/>", RegexOptions.Singleline);
    if (!metaMatch.Success) return null;

    var attrs = metaMatch.Groups[1].Value;
    return new SearchEntry
    {
        Url = url,
        Title = ExtractAttr(attrs, "Title") ?? Path.GetFileNameWithoutExtension(razorFile),
        Section = ExtractAttr(attrs, "Section"),
        Status = ExtractAttr(attrs, "Status")?.Replace("DocPageStatus.", "") ?? "Draft",
        Description = ExtractAttr(attrs, "Description"),
        Keywords = ExtractAttr(attrs, "Keywords"),
        TestedAgainst = ExtractAttr(attrs, "TestedAgainst"),
    };
}

static string? ResolveFrameworkVersion(string solutionRoot)
{
    // Look for ShiftTemplates/ShiftFrameworkGlobalSettings.props next to ShiftFrameworkDocumentation.
    // (Both repos live as siblings under the ShiftSoftware org folder.)
    var candidates = new[]
    {
        Path.Combine(solutionRoot, "..", "ShiftTemplates", "ShiftFrameworkGlobalSettings.props"),
    };
    foreach (var candidate in candidates)
    {
        var resolved = Path.GetFullPath(candidate);
        if (!File.Exists(resolved)) continue;
        try
        {
            var xml = File.ReadAllText(resolved);
            var m = Regex.Match(xml, @"<ShiftFrameworkVersion>([^<]+)</ShiftFrameworkVersion>");
            if (m.Success) return m.Groups[1].Value.Trim();
        }
        catch { }
    }
    return null;
}

static string? ExtractAttr(string attrs, string name)
{
    var m = Regex.Match(attrs, $@"{name}\s*=\s*""([^""]*)""");
    return m.Success ? m.Groups[1].Value : null;
}

static string? FindSolutionRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        if (dir.GetFiles("*.sln").Any()) return dir.FullName;
        dir = dir.Parent;
    }
    return null;
}

class SearchEntry
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Section { get; set; }
    public string Status { get; set; } = "Draft";
    public string? Description { get; set; }
    public string? Keywords { get; set; }
    public string? TestedAgainst { get; set; }
}
