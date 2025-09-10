using System.Text.Json;

var solutionRoot = FindSolutionRoot() ?? Directory.GetCurrentDirectory();
var docsWebRoot = Path.Combine(solutionRoot, "Docs.Web");
var outRoot = Path.Combine(docsWebRoot, "wwwroot", "snippets");

Console.WriteLine("==========");

var sources = new[]
{
    Path.Combine(docsWebRoot, "Pages", "Components")
}.Where(Directory.Exists).ToArray();

Console.WriteLine("Snippet Generator started");

Directory.CreateDirectory(outRoot);

foreach (var src in sources)
{
    // All .razor files under subfolders (not directly under Components), excluding Index.razor
    var files = Directory.EnumerateFiles(src, "*.razor", SearchOption.AllDirectories)
        .Where(full =>
        {
            var fileName = Path.GetFileName(full);
            if (fileName.Equals("Index.razor", StringComparison.OrdinalIgnoreCase) || fileName.Equals("IndexAPI.razor", StringComparison.OrdinalIgnoreCase))
                return false;

            var fileDir = Path.GetFullPath(Path.GetDirectoryName(full)!)
                              .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var srcDir = Path.GetFullPath(src)
                              .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return !string.Equals(fileDir, srcDir, StringComparison.OrdinalIgnoreCase);
        });

    foreach (var file in files)
        Process(file, src, outRoot);
}

Console.WriteLine("[SnippetGen] done");
return;

static void Process(string full, string srcRoot, string outRoot)
{
    var baseName = Path.GetFileNameWithoutExtension(full);
    var outFile = $"{baseName}.txt";

    var outPath = Path.Combine(outRoot, outFile);

    var text = File.ReadAllText(full);
    File.WriteAllText(outPath, text);
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
