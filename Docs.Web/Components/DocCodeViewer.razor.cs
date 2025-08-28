using Docs.Web.Interfaces;
using Microsoft.AspNetCore.Components;
using static System.Net.WebRequestMethods;

namespace Docs.Web.Components
{
    public partial class DocCodeViewer
    {
        [Inject] private NavigationManager Nav { get; set; } = default!;
        [Inject] private HttpClient Http { get; set; } = default!;
        [Parameter] public required Type DocComponent { get; set; }
        private bool ShowCode;
        private IDocumentedComponent? instance;

        private string? snippet;

        private void ToggleCode()
        {
            ShowCode = !ShowCode;
        }

        private async void getTextCode(string fileName)
        {
            var url = new Uri(Nav.BaseUri + "snippets/ShiftAutoCompleteBasicUse.txt");
            snippet = await Http.GetStringAsync(url);

            //Console.WriteLine(snippet);
        } 

        protected override void OnInitialized()
        {
            if (DocComponent != null)
            {
                instance = Activator.CreateInstance(DocComponent) as IDocumentedComponent;

                var type = DocComponent;
                var fileName = type.Name + ".razor";

                getTextCode(fileName);
            }
        }
    }
}
 