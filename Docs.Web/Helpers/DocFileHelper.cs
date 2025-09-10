using Docs.Web.Interfaces;
using Microsoft.AspNetCore.Components;

namespace Docs.Web.Helpers;

public class DocFileHelper
{
    private readonly HttpClient _http;
    private readonly NavigationManager _nav;

    public DocFileHelper(HttpClient http, NavigationManager nav)
    {
        _http = http;
        _nav = nav;
    }

    public async Task<CodeFile> GetDocFile<T>(
        bool? downloadable = true, string? prismClass = "language-razor", string? overrideName = null, string? lineheighlight = "")
    {
        return await GetDocFile(typeof(T), downloadable, prismClass, overrideName, lineheighlight);
    }

    public async Task<CodeFile> GetDocFile(
    Type ComponentType, bool? downloadable = true, string? prismClass = "language-razor", string? overrideName = null, string? lineheighlight = "")
    {
        prismClass = prismClass ?? "language-razor";

        var txtName = (overrideName ?? ComponentType.Name) + ".txt";
        var url = new Uri(_nav.BaseUri + "snippets/" + txtName);

        var snippet = await _http.GetStringAsync(url);

        return new CodeFile
        {
            Content = snippet,
            PrismClass = prismClass,
            Downloadable = downloadable ?? true,
            Linehighlight = lineheighlight ?? "",
            FileName = txtName.Replace(".txt", this.InferExtensionFromPrism(prismClass))
        };
    }


    private string InferExtensionFromPrism(string prismClass) =>
            prismClass switch
            {
                "language-razor" => ".razor",
                "language-cs" => ".cs",
                "language-ts" => ".ts",
                "language-js" => ".js",
                "language-css" => ".css",
                _ => ".txt"
            };
}
