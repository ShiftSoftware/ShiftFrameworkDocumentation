using Docs.Web.Helpers;
using Docs.Web.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace Docs.Web.Components
{
    public partial class CodeViewer
    {
        [Inject] private IJSRuntime JS { get; set; } = default!;
        [Inject] private ISnackbar Snackbar { get; set; } = default!;
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

        private async Task CopyToClipboard(string TextToCopy)
        {
            try
            {
                await JS.InvokeVoidAsync("navigator.clipboard.writeText", TextToCopy);

                Snackbar.Add("Code copied successfully!", Severity.Success);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Snackbar.Add("Failed to copy code.", Severity.Error);
            }

            StateHasChanged();
        }

        private async Task DownloadFile(string fileName, string content)
        {
            try
            {
                await JS.InvokeVoidAsync("downloadTextFile", fileName, content);
                Snackbar.Add($"File \"{fileName}\" downloaded.", Severity.Success);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Snackbar.Add("Download failed.", Severity.Error);
            }
        }
    }
}
