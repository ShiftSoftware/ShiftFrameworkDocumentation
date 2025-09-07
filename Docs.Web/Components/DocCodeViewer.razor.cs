using Docs.Web.Interfaces;
using Microsoft.AspNetCore.Components;

namespace Docs.Web.Components
{
    public partial class DocCodeViewer
    {
        [Inject] private NavigationManager Nav { get; set; } = default!;
        [Inject] private HttpClient Http { get; set; } = default!;
        [Parameter] public required Type DocComponent { get; set; }
        [Parameter] public bool downloadable{ get; set; } = true;
        [Parameter] public string Linehighlight { get; set; } = "";

        private bool ShowCode;

        private CodeFile? ComponenCodeFile { get; set; }

        private void ToggleCode()
        {
            ShowCode = !ShowCode;
        }

        private async void getTextCode(string fileName)
        {
            var url = new Uri(Nav.BaseUri + "snippets/" + fileName);
            var snippet = await Http.GetStringAsync(url);
            
            Console.WriteLine($"Loading snippet from {Linehighlight}");

            ComponenCodeFile = new CodeFile
            {
                Content = snippet,
                Downloadable = downloadable,
                Linehighlight= Linehighlight,
                PrismClass = "language-razor",
                FileName = fileName.Replace(".txt", ".razor"),
            };

        } 

        protected override void OnInitialized()
        {
            if (DocComponent != null)
            {
                var fileName = DocComponent.Name + ".txt";

                getTextCode(fileName);
            }
        }
    }
}
 