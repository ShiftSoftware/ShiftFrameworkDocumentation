using Docs.Web.Interfaces;

namespace Docs.Web.Pages.Components.ShiftAutoComplete
{
    public partial class ShiftAutocompleteClearable : IDocumentedComponent
    {
        public List<CodeFile> Docs => new()
                {
                    new CodeFile {
                        Downloadable = true,
                        Linehighlight = "6",
                        PrismClass = "language-razor",
                        FileName = "ShiftAutoCompleteClearable.razor",
                        Content = @"
    @using Docs.Shared.Customers
    @using ShiftSoftware.ShiftBlazor.Components

    <ShiftAutocomplete EntitySet=""Customer""
                        Label=""Customer""
                        Clearable
                        TEntitySet=""CustomerListDTO"" />"
                    },
                };
    }
}
