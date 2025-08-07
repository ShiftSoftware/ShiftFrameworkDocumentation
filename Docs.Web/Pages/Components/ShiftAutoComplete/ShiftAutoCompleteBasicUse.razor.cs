using Docs.Web.Interfaces;

namespace Docs.Web.Pages.Components.ShiftAutoComplete
{
    public partial class ShiftAutoCompleteBasicUse : IDocumentedComponent
    {
        public List<CodeFile> Docs => new()
            {
                new CodeFile { FileName = "ShiftAutoCompleteBasicUsage.razor", PrismClass = "language-razor", Downloadable = true,
                    Content = @"
@using ShiftSoftware.ShiftBlazor.Components
@using Docs.Shared.Customers

<ShiftAutocomplete EntitySet=""Customer""
                   Label=""Customer""
                   TEntitySet=""CustomerListDTO"" />"
                },
            };
    }
}
