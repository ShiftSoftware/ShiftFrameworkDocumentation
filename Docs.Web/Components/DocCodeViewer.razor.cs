using Docs.Web.Interfaces;
using Microsoft.AspNetCore.Components;

namespace Docs.Web.Components
{
    public partial class DocCodeViewer
    {
        [Parameter] public required Type DocComponent { get; set; }
        private bool ShowCode;
        private IDocumentedComponent? instance;

        private void ToggleCode()
        {
            ShowCode = !ShowCode;
        }

        protected override void OnInitialized()
        {
            if (DocComponent != null)
            {
                instance = Activator.CreateInstance(DocComponent) as IDocumentedComponent;
            }
        }
    }
}
