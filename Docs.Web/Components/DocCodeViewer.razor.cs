using Docs.Web.Helpers;
using Docs.Web.Interfaces;
using Microsoft.AspNetCore.Components;

namespace Docs.Web.Components
{
    public partial class DocCodeViewer
    {
        [Inject] private DocFileHelper DocFiles { get; set; } = default!;
        [Parameter] public required Type DocComponent { get; set; }
        [Parameter] public bool downloadable{ get; set; } = true;
        [Parameter] public string Linehighlight { get; set; } = "";
        [Parameter] public RenderFragment? PreCodeSnippet { get; set; }
        [Parameter] public RenderFragment? PostCodeSnippet { get; set; }

        private bool ShowCode;

        private CodeFile? ComponenCodeFile { get; set; }

        private void ToggleCode()
        {
            ShowCode = !ShowCode;
        }

        private async void getDocComponentCodeFie()
        {   
            ComponenCodeFile = await DocFiles.GetDocFile(DocComponent, downloadable, lineheighlight: Linehighlight);
        } 

        protected override void OnInitialized()
        {
            if (DocComponent != null)
            {
                getDocComponentCodeFie();
            }
        }
    }
}
 