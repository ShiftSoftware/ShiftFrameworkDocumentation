# CLAUDE.md

This file provides guidance to Claude Code (and human contributors) when working in the ShiftFrameworkDocumentation repo.

## What This Repository Is

The Shift Framework documentation site, built as a Blazor WebAssembly app and hosted at **https://docs.shift.software** on a Windows VPS via IIS.

The audience is **new developers from external organizations** who have never seen the framework before and need to go from zero to a working app.

## Project Layout

| Project | Purpose |
|---|---|
| `Docs.Web` | Blazor WASM frontend — pages, components, layout. All documentation content lives here. |
| `Docs.API` | ASP.NET Core host that serves the Blazor WASM bundle and provides backing API endpoints for live component demos. **This is what you run for local development.** |
| `Docs.Data` | EF Core DbContext + entities + repositories for the demo data behind `Docs.API`. |
| `Docs.Shared` | DTOs and types shared between `Docs.API` and `Docs.Web`. |
| `Docs.SnippetGen` | Build-time tool that scans source files for snippet regions, copies Razor demo files into `Docs.Web/wwwroot/snippets/`, and generates `wwwroot/search-index.json` from `<DocPageMeta />` declarations. Runs automatically as a `BeforeTargets="Build"` step on `Docs.Web`. |

## Running Locally

```bash
dotnet run --project Docs.API
```

**Always run `Docs.API`, not `Docs.Web` directly.** `Docs.API` is the ASP.NET Core host that includes the Blazor WASM dev server. Running `Docs.Web` standalone is not the team's workflow and will not give you the full experience (no API for live demos, etc.).

## Active Plan

**Single source of truth for ongoing documentation work:** [`PLAN.md`](PLAN.md)

Always check `PLAN.md` before starting work to see current status, decisions, open questions, and known issues. Update it whenever:
- A workstream item is completed.
- A decision is made on an open question.
- Scope changes.

## Authoring Documentation Pages

All pages are pure `.razor` (no Markdown). Every doc page should:

1. Declare its route with `@page`.
2. Include exactly one `<DocPageMeta />` at the top with `Title`, `Section`, `Status`, `Description`, and `Keywords`. This single declaration drives the page header, browser title, status badge, and search index entry — no duplication.
3. Use the helper components for consistent layout:
   - `<DocSection Title="...">` — section heading with auto anchor link.
   - `<DocCallout Type="DocCalloutType.Note|Tip|Warning|Danger">` — colored callout boxes.
   - `<DocSnippet Name="..." Language="cs" />` — render a named snippet extracted by `Docs.SnippetGen`.

### Snippet Markers

To pull a region of source code into a doc page, mark it in the source file:

**C# (`.cs`):**
```csharp
// snippet:UpsertEndpoint
public async Task<ActionResult> Upsert(MyDTO dto) { ... }
// endsnippet
```

**Razor (`.razor`):**
```razor
@* snippet:CustomerForm *@
<MudTextField @bind-Value="customer.Name" />
@* endsnippet *@
```

**Bash (`.sh` / `.bash`):**
```bash
# snippet:InstallTemplate
dotnet new install ShiftSoftware.ShiftTemplates
# endsnippet
```

Then reference it from a doc page: `<DocSnippet Name="UpsertEndpoint" Language="cs" />`.

Snippet names must be **globally unique** — `Docs.SnippetGen` fails the build on duplicates. SnippetGen scans `Docs.Web/Pages/`, `Docs.API/`, `Docs.Data/`, `Docs.Shared/`, and `Docs.Verify/` for region markers.

**Everything between the markers is shown to readers verbatim.** That includes any comments, blank lines, or whitespace inside the region. If you want to leave a note for yourself or future maintainers about *why* a command exists, put the note **outside** the snippet markers — otherwise it leaks into the rendered code block on the doc page.

```bash
# Author note (NOT shown to readers — outside the snippet block).
# Explain the why of the next command here.

# snippet:DoTheThing
dotnet do-the-thing
# endsnippet
```

## Keeping Docs in Sync With Reality

We follow a **two-tier verification model** so the docs don't drift from the framework:

**Tier 1 — `TestedAgainst` metadata.** Every non-Draft page must declare `TestedAgainst="<framework version>"` on its `<DocPageMeta />`. The page header renders a small "Last verified against Shift Framework v..." badge so readers always know how fresh a page is. `Docs.SnippetGen` reads the current framework version from `ShiftTemplates/ShiftFrameworkGlobalSettings.props` and emits a build warning for any non-Draft page whose `TestedAgainst` is missing or behind the current version.

**Tier 2 — Anchored commands and code.** Every command, code snippet, and config example shown to readers comes from a real source file in `Docs.Verify/` (or one of the other scanned roots), never typed inline into a `.razor` page. Pages reference snippets via `<DocSnippet Name="..." />`. This makes it structurally impossible for a command in the docs to differ from a command that was actually verified.

**Workflow when verifying a page:**
1. Walk through the page on a clean machine.
2. Fix anything that's wrong by editing the **anchored source** (in `Docs.Verify/Scripts/...`), not the doc page.
3. Bump the page's `Status` to `DocPageStatus.Reviewed` or `Stable`.
4. Set `TestedAgainst` to the current framework version (the value of `ShiftFrameworkVersion` in `ShiftTemplates/ShiftFrameworkGlobalSettings.props`).

A future Tier 3 will run the anchored scripts in CI on every PR. For now the contract is: scripts in `Docs.Verify/` are manually verified, and the doc only shows what they contain.

### Page Status

- `DocPageStatus.Draft` — work in progress, may be inaccurate. Shows a yellow chip.
- `DocPageStatus.Reviewed` — content complete, has had editorial review. Shows a blue chip.
- `DocPageStatus.Stable` — production-ready, validated against current framework version. No chip (default expectation).

## Key Conventions

- **No Markdown.** Pure `.razor` everywhere.
- **No backwards-compatibility shims for the docs site itself.** It's a single-version, latest-only site.
- **The site is public.** No auth gate. Identity wiring in `Docs.Web/Program.cs` is leftover scaffolding from the `shift` template — it can be removed if it gets in the way.
- **Snippet collisions fail the build.** This is intentional — silent collisions would let pages show the wrong code.
