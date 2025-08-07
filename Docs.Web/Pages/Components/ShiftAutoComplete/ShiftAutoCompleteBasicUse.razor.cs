using Docs.Web.Interfaces;

namespace Docs.Web.Pages.Components.ShiftAutoComplete
{
    public partial class ShiftAutoCompleteBasicUse : IDocumentedComponent
    {
        public List<CodeFile> Docs => new()
            {
                new CodeFile { 
                    Downloadable = true,
                    PrismClass = "language-razor", 
                    FileName = "ShiftAutoCompleteBasicUsage.razor", 
                    Content = @"
@using Docs.Shared.Customers
@using ShiftSoftware.ShiftBlazor.Components

<ShiftAutocomplete EntitySet=""Customer""
                   Label=""Customer""
                   TEntitySet=""CustomerListDTO"" />"
                },
            };
    }
}
