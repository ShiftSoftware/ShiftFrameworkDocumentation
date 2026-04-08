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
2. Include exactly one `<DocPageMeta />` at the top with `Title`, `Section`, `Order`, `Status`, `Description`, and `Keywords`. This single declaration drives the page header, browser title, status badge, side nav position, and search index entry — no duplication. **Avoid `<` and `>` inside attribute values** (e.g. don't write `Description="ShiftEntity<T>"`) — the SnippetGen regex will warn but the page will still parse correctly only if the `<DocPageMeta>` tag is otherwise valid. Prefer plain prose ("the ShiftEntity base class") in these fields.

   **`Order`** is a numeric sort key for the side nav within a section. Lower values appear first. Pages without `Order` fall back to alphabetical-by-title at the end of the section. **Convention: use multiples of 10** (10, 20, 30, ...) so future inserts have room. Each section maintains its own number space — Get Started can have 10/20/30 alongside Concepts having 10/20/30, they don't conflict.
3. Use the helper components for consistent layout:
   - `<DocSection Title="...">` — section heading with auto anchor link.
   - `<DocCallout Type="DocCalloutType.Note|Tip|Warning|Danger">` — colored callout boxes.
   - `<DocSnippet Name="..." Language="cs" />` — render a named snippet extracted by `Docs.SnippetGen` from a real source file (preferred for verified, anchored code).
   - `<DocCodeBlock Language="csharp" Code="@(@"...")" />` — inline code block for **illustrative** code that doesn't come from an anchored source file. Use this for any code sample containing `<`, `>`, or generic type arguments — Razor's parser tries to parse `<` as a tag opener even inside `<pre><code>`, so passing the code as a string parameter is the only reliable way to embed C# generics, comparisons, or HTML markup. For verified, anchored code, prefer `<DocSnippet>` instead.

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
1. Run the verification script in `Docs.Verify/Scripts/<page>.sh` end-to-end. If the script doesn't exist, write it. If it fails, fix the script — never the doc page directly.
2. Once the script runs cleanly, bump the page's `Status` to `DocPageStatus.Reviewed` or `Stable`.
3. Set `TestedAgainst` to the current framework version (the value of `ShiftFrameworkVersion` in `ShiftTemplates/ShiftFrameworkGlobalSettings.props`).

### Authoring verification scripts (Tier 2.5)

A verification script does **two jobs in one file**:
1. Anchors the user-facing commands the doc shows (via `# snippet:...` markers)
2. Actually executes those commands end-to-end as a verification harness

Most commands work for both jobs (e.g. `dotnet ef database update` is the same whether the user runs it or the script runs it). But some commands — especially `dotnet run`, which blocks until killed — would freeze the script forever if executed as bash. Park those inside a function that's defined but never called:

```bash
# This function exists only to anchor commands for the docs.
# It is never actually called — the verification harness below runs equivalent
# operations in a background-friendly way.
docs_only_anchors() {
    # snippet:RunApi
    dotnet run --project MyFirstShiftApp.API
    # endsnippet
}

# Verification harness: do the same thing, but in a way that lets the script proceed.
dotnet run --project MyFirstShiftApp.API --no-launch-profile --urls "http://localhost:5079" > /tmp/api.log 2>&1 &
API_PID=$!
# ... wait for port, probe, kill ...
```

SnippetGen extracts the body of the never-called function just fine. The doc page renders the user-facing command. The verification harness runs the script-friendly version. They stay in sync because they live in the same file.

**Other rules for verification scripts:**
- Use `set -uo pipefail` (NOT `-e`) so trap-based error handling works cleanly.
- Wrap pause prompts in `if [ -t 0 ]` checks so non-interactive runs (CI, parent shells) don't hang waiting for input.
- Always have an `EXIT` trap that cleans up any background processes and any state created (databases, scratch folders, etc).
- For database verification, use a unique DB name per run (`DB_NAME="MyFirstShiftAppVerify_$(date +%s)"`) and drop it on exit so reruns are reproducible.
- Override config via environment variables (`ConnectionStrings__SQLServer=...`, `ASPNETCORE_ENVIRONMENT=Development`) rather than editing the scaffolded files — keeps the user's natural flow uncontaminated.

A future Tier 3 will run these scripts in CI on every PR. For now the contract is: scripts in `Docs.Verify/` are manually verified by Claude before a page is marked Reviewed, and the doc only shows what they contain.

### Page Status

- `DocPageStatus.Draft` — work in progress, may be inaccurate. Shows a yellow chip.
- `DocPageStatus.Reviewed` — content complete, has had editorial review. Shows a blue chip.
- `DocPageStatus.Stable` — production-ready, validated against current framework version. No chip (default expectation).

## Key Conventions

- **No Markdown.** Pure `.razor` everywhere.
- **No backwards-compatibility shims for the docs site itself.** It's a single-version, latest-only site.
- **The site is public.** No auth gate. Identity wiring in `Docs.Web/Program.cs` is leftover scaffolding from the `shift` template — it can be removed if it gets in the way.
- **Snippet collisions fail the build.** This is intentional — silent collisions would let pages show the wrong code.
