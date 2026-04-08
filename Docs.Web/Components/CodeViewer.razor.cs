using Docs.Web.Helpers;
using Docs.Web.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Docs.Web.Components
{
    public partial class CodeViewer
    {
        [Inject] private IJSRuntime JS { get; set; } = default!;
        [Inject] private DocFileHelper DocFiles { get; set; } = default!;

        [Parameter] public CodeFile? file { get; set; }
        [Parameter] public Type? ComponentType { get; set; }

        protected override async Task OnParametersSetAsync()
        {
            if (file is null && ComponentType is not null)
            {
                file = await DocFiles.GetDocFile(ComponentType);
            }
            await JS.InvokeVoidAsync("Prism.highlightAll");
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await JS.InvokeVoidAsync("Prism.highlightAll");
            }
        }
    }
}
