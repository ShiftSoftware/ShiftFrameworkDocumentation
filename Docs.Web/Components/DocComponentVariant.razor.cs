using Microsoft.AspNetCore.Components;

namespace Docs.Web.Components
{
    public partial class DocComponentVariant
    {
        [Parameter] public string? Class { get; set; }
        [Parameter] public string? Title { get; set; } = default;
        [Parameter] public RenderFragment? TitleContent { get; set; }
        [Parameter] public RenderFragment? ChildContent { get; set; }
        [Parameter] public RenderFragment? PostPreviewContent { get; set; }
        [Parameter] public required Type DocComponent { get; set; }
    }
}
