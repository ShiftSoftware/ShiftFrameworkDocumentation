namespace Docs.Web.Interfaces
{
    public interface IDocumentedComponent
    {
        List<CodeFile> Docs { get; }
    }

    public class CodeFile
    {
        public required string FileName { get; set; }
        public required string Content { get; set; }
        public required string PrismClass { get; set; }
        public required bool Downloadable { get; set; }
        public string? Linehighlight { get; set; } = default; // https://prismjs.com/plugins/line-highlight/

    }

}

