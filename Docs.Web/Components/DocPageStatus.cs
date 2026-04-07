namespace Docs.Web.Components;

/// <summary>
/// Editorial status of a documentation page. Visible to readers as a colored chip
/// near the page title so unfinished pages don't masquerade as authoritative.
/// </summary>
public enum DocPageStatus
{
    /// <summary>Work in progress, may be inaccurate or incomplete.</summary>
    Draft,
    /// <summary>Content is complete and has had at least one editorial pass.</summary>
    Reviewed,
    /// <summary>Production-ready, validated against the current framework version.</summary>
    Stable,
}
