## Purpose

Build production-ready documentation for the Shift Framework, targeted at **new developers from external organizations** who have never seen the framework before and need to go from zero to a working app.

This is a fresh planning document. Existing content in `Docs.Web` is treated as loose reference, not a foundation to preserve.

## North Star

A new developer lands on the site and within an hour can:
1. Understand what Shift Framework is and whether it fits their use case.
2. Scaffold a working project using `dotnet new shift`.
3. Add their first entity (DTO + repository + controller + Blazor page) and see it work end-to-end.
4. Know where to look next when they hit a real-world need (auth, files, lookups, OData, multi-tenancy, etc.).

If any of those four steps requires reading source code, the docs have failed.

## Current State (audit, 2026-04-07)

**Infrastructure (mostly built):**
- `Docs.Web` — Blazor WASM site, MudBlazor + ShiftBlazor, multi-language plumbing (en/ar/ku).
- `Docs.API` / `Docs.Data` / `Docs.Shared` — backing API + EF data project so live samples can hit real endpoints.
- `Docs.SnippetGen` — extracts `.razor` files under `Pages/Components/**` into `wwwroot/snippets/*.txt`, consumed by `DocCodeViewer`.
- `DocFileHelper`, `AutoComponentDocs`, `DocComponentVariant` — components for showing live demos alongside source.

**Content (almost nothing):**
- `Pages/Documentation/`: only `Overview.razor` and `Installation.razor` (stubs).
- `Pages/Components/`: only `ShiftAutoComplete` has real variants. `FileExplorerDoc` and `ShiftBlazor` exist as placeholders.
- No coverage of: ShiftEntity, ShiftIdentity, TypeAuth, ShiftRepository, OData, Cosmos replication, file storage, localization, the `shift` template itself, or any of the API-side concepts.

**Gaps in the system itself:**
- No information architecture — no defined sections, no nav taxonomy, no "getting started" path.
- No search.
- No versioning story (docs are tied to whatever package versions Docs.Web references).
- No contributor guide for adding a new doc page.
- SnippetGen only handles component variants under `Pages/Components`; no equivalent for backend code samples (controllers, repositories, entities).
- Component API reference (`IndexAPI.razor`) is hand-written per component — doesn't scale.

## Scope of This Plan

In scope:
- Define the information architecture and audience journey.
- Decide what content must exist for v1 ("production ready") and in what order to write it.
- Identify tooling gaps that block content authoring and fix them before writing at scale.
- Establish a contributor workflow so docs stay in sync with framework changes.

Out of scope (for now):
- Visual redesign of the site beyond what's needed for navigation clarity.
- Translating content to ar/ku — keep the language plumbing, but ship en-US first.
- Migrating off Blazor WASM. The current stack stays.

## Proposed Information Architecture

Five top-level sections, in the order a new developer encounters them:

1. **Get Started** — what is Shift, install prerequisites, `dotnet new shift`, project tour, run the sample, "your first entity" walkthrough. Linear, opinionated, no choices.
2. **Concepts** — the mental model: ShiftEntity, repositories, DTOs (View/List/Upsert), the mapping abstraction, TypeAuth, ShiftIdentity integration, OData query pipeline, multi-tenancy, soft delete + audit fields. Prose-heavy, diagrams where they earn their keep.
3. **Guides (How-to)** — task-oriented recipes: "add a lookup field", "upload files to Azure Storage", "add a Cosmos replica", "wire up TypeAuth permissions", "expose an OData endpoint", "write a custom mapper", "add a background job". Each guide is self-contained and copy-pasteable.
4. **Components** — ShiftBlazor component reference. Live demos + variants + API table. This is what `Pages/Components` already targets; expand it.
5. **API Reference** — auto-generated from XML docs where possible. Lower priority for v1, but architect the nav slot now.

A separate **Contributing** section (not in main nav) covers how to add/edit docs.

## Verification Model (Tier 1 + Tier 2, shipped 2026-04-07)

To prevent docs from drifting out of sync with the framework, we use a layered verification model. Detailed workflow is in `CLAUDE.md`.

- **Tier 1 — `TestedAgainst` metadata.** Every non-Draft page declares `TestedAgainst="<framework version>"` on `<DocPageMeta />`. Page header shows a "Last verified against Shift Framework v..." badge. `Docs.SnippetGen` reads the current version from `ShiftTemplates/ShiftFrameworkGlobalSettings.props` and warns on any non-Draft page that's missing the field or behind. ✅ Built.
- **Tier 2 — Anchored commands and code.** Every command/code snippet shown to readers comes from `Docs.Verify/` or another scanned source root, never typed inline. Doc pages reference snippets via `<DocSnippet Name="..." />`. Structurally prevents drift on the code side. ✅ Built. SnippetGen now scans `Docs.Verify/` and supports `.sh` / `.bash` files with `# snippet:Name` markers.
- **Tier 3 — Executable doctests in CI** (deferred). A future `Docs.Verify` executable project will run the anchored scripts in a clean container on every PR. Revisit when there are 20+ pages worth protecting.
- **Tier 4 — Cadenced manual review.** Once per framework release, a human walks the Get Started flow on a clean VM and bumps `TestedAgainst` on every Stable page. This is the only thing that catches prose errors no automated system can find.

## Workstreams

### A. Information architecture & nav
- [x] **Done 2026-04-07.** Five-section taxonomy locked: Get Started / Concepts / Guides / Components / API Reference.
- [x] **Done 2026-04-07.** URL structure defined: `/get-started/...`, `/concepts/...`, `/guides/...`, `/components/...`, `/api/...`. Section name → URL prefix is mechanical (lowercase, spaces → dashes).
- [x] **Done 2026-04-07.** Rewrote `NavMenu.razor` as **data-driven**: reads `wwwroot/search-index.json` via `SearchService.GetAllGroupedBySectionAsync(SectionOrder)` and renders pages grouped by their `<DocPageMeta Section="..." />` declaration. New pages auto-appear in the nav with zero nav editing — just declare DocPageMeta. Draft / Reviewed status chips render inline next to each link. Section auto-expands when the user is browsing inside it. Legacy pages without DocPageMeta simply don't appear yet — they get migrated as we touch them.
- [ ] Add breadcrumbs and "previous / next" links inside Get Started so the linear flow is obvious. _(Deferred — needs an explicit page-order declaration; revisit when there are 3+ Get Started pages.)_

### B. Tooling & authoring experience (do before writing at scale)
- [x] **Done 2026-04-07.** Extended `Docs.SnippetGen` to scan `Docs.API`, `Docs.Data`, `Docs.Shared`, and `Docs.Web/Pages` in addition to the existing `Pages/Components` whole-file mode. Added named-region extraction with `// snippet:Name ... // endsnippet` (C#) and `@* snippet:Name *@ ... @* endsnippet *@` (Razor) markers. Indent-stripped, duplicate-name detection, fails the build on collision. Also added `DocFileHelper.GetSnippetByName(...)` for the upcoming `<DocSnippet />` helper.
- [x] **Decided 2026-04-07.** Pure `.razor` for all pages. No Markdown pipeline.
- [x] **Done 2026-04-07.** Built three authoring helper components: `<DocSection Title="..." />` (consistent heading + auto anchor + slug), `<DocCallout Type="Note|Tip|Warning|Danger" Title="..." />` (MudAlert-backed colored boxes with icons), `<DocSnippet Name="..." Language="cs" Highlight="..." />` (fetches a named region from `wwwroot/snippets/` and renders via existing `CodeViewer`). All three live under `Docs.Web/Components/` and are usable everywhere via the existing `_Imports.razor`.
- [x] **Done 2026-04-07.** `AutoComponentDocs` already does reflection-based parameter discovery; extended it with XML-doc-driven descriptions. New `XmlDocService` lazily fetches `wwwroot/xmldocs/{AssemblyName}.xml`, parses `<member>` entries, and resolves `<summary>` text by member key. Wired into `ComponentDocService.DescribeAsync` as the default description fallback. MSBuild target `CopyShiftFrameworkXmlDocs` copies `ShiftSoftware.*.xml` from `OutDir` into `wwwroot/xmldocs/` after build, and `<CopyDocumentationFilesFromPackages>true</CopyDocumentationFilesFromPackages>` is set to surface XML docs from NuGet packages.
  - **Known gap:** ShiftBlazor's NuGet packages currently do **not** ship XML doc files. Verified across 5 package versions in `~/.nuget/packages/shiftsoftware.shiftblazor/`. The plumbing is in place and will activate automatically the moment upstream packages start shipping `.xml`. Until then, descriptions stay null unless authors hand-add `[Description]` attributes. Follow-up: open an issue/PR on ShiftBlazor to enable `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and ensure the resulting `.xml` is packed.
- [x] **Done 2026-04-07.** Client-side search shipped. `Docs.SnippetGen` scans every `.razor` file under `Docs.Web/Pages/` for `<DocPageMeta />` declarations + the `@page` route, and writes `wwwroot/search-index.json` at build time. New `SearchService` lazily loads the index and serves crude weighted substring matching (title > keywords > section > description). New `<DocSearchBox />` MudAutocomplete component provides the UI — drop into MainLayout when ready. Currently produces an empty index because no pages declare meta yet; activates the moment Get Started pages are written.
- [x] **Done 2026-04-07.** Page-status badge shipped via `<DocPageMeta />`. Single component handles three responsibilities: (1) renders `<PageTitle>`, (2) renders the page heading + a colored MudChip showing Draft/Reviewed (Stable hides the chip since "stable" is the default expectation), (3) provides metadata that SnippetGen scans into the search index. `DocPageStatus` enum: Draft / Reviewed / Stable. Authors place one `<DocPageMeta>` at the top of every doc page; the same component drives both the badge and the search index — no duplicate metadata.

### C. Content — Get Started (highest priority)
- [x] **Done 2026-04-07.** What is Shift Framework? — `Pages/GetStarted/WhatIsShiftFramework.razor`. Status: Draft. Validated all the new tooling end-to-end (DocPageMeta + DocSection + DocCallout + search index entry generation). Diagram of the moving pieces still TODO — placeholder is the bullet list under "What you get".
- [x] **Done + Verified 2026-04-07.** Prerequisites & install — `Pages/GetStarted/Installation.razor`. Status: **Reviewed**, TestedAgainst: **2026.04.3.1**. Walked through end-to-end via `Docs.Verify/Scripts/install-and-scaffold.sh`. All commands anchored. Verification surfaced and corrected several issues: bogus Node.js prerequisite removed; required `--includeSampleApp` and `--shiftIdentityHostingType` parameters added; deprecated `::*` upgrade syntax replaced with `@*`; `dotnet build .sln` workaround documented (SDK 10.0.201 + template-generated `.sln` produces silently empty builds — see Known Issues).
- [ ] `dotnet new shift` walkthrough — every parameter explained (`includeSampleApp`, `shiftIdentityHostingType`, `addFunctions`, `addTest`, mapping strategy)
- [ ] Project tour — what each project does (API / Data / Shared / Web / Functions / Test) and how they relate
- [ ] Run the generated sample — first run, default credentials, what to click
- [ ] Your first entity — full walkthrough adding a new entity end-to-end (entity → DTO → repository → controller → Blazor list/form page), using the `shiftentity` item template

### D. Content — Concepts
- [ ] ShiftEntity model (ID, audit fields, soft delete, ReloadAfterSave)
- [ ] DTO triad: View / List / Upsert — why, when each is used
- [ ] Repositories and the request lifecycle
- [ ] Mapping abstraction (`IShiftEntityMapper`) — AutoMapper / Manual / Mapperly / Mapster, when to pick which
- [ ] TypeAuth — action trees, permission checks, integration with controllers and Blazor
- [ ] ShiftIdentity — internal vs external hosting, user/role model, sync flow
- [ ] OData query pipeline — how list endpoints work, what's translatable, what isn't
- [ ] Localization
- [ ] Multi-tenancy / company isolation (if applicable — confirm scope)
- [ ] Cosmos DB replication

### E. Content — Guides
- Initial set (10 guides). Each sourced from a real question external developers are likely to ask:
  - [ ] Add a lookup field with `ShiftEntitySelectDTO`
  - [ ] Upload and store files (`ShiftFileDTO` + Azure Storage)
  - [ ] Define and enforce TypeAuth permissions
  - [ ] Add a custom OData filter / expand
  - [ ] Write a manual mapper for a complex entity
  - [ ] Add an Azure Function timer job
  - [ ] Replicate an entity to Cosmos
  - [ ] Add a Blazor list + form for an existing entity
  - [ ] Customize the ShiftIdentity user/role model
  - [ ] Deploy: API + Web + Functions to Azure (reference deployment)

### F. Content — Components
- [ ] Inventory ShiftBlazor's public components, prioritize by usage in the StockPlusPlus sample.
- [ ] For each priority component: overview, basic use variant, 2–4 advanced variants, API table.
- [ ] Migrate `ShiftAutoComplete` (already done) into the new template/structure as the canonical example.

### G. Contributor workflow
- [ ] `CONTRIBUTING.md` in this repo: how to add a page, how snippets work, how to mark page status, how to run locally.
- [ ] Add a "doc impact" reminder to the framework repos' CLAUDE.md files: when changing public API in ShiftEntity / ShiftBlazor / ShiftIdentity, check whether a docs page needs updating.
- [ ] Decide on a versioning policy: does each docs deploy correspond to a framework version tag? If yes, document the release process.

## Decisions

- **Page format:** Pure `.razor` for all pages (Get Started, Concepts, Guides, Components). No Markdown pipeline. Every contributor is expected to know Blazor.
- **Prose helper components:** Build a small set of authoring helpers up front so pages stay uniform without verbose markup:
  - `<DocSection Title="...">` — consistent heading + spacing + anchor link.
  - `<DocCallout Type="Note|Warning|Tip">` — standard colored callout boxes.
  - `<DocSnippet Name="..." />` — thin wrapper over `DocCodeViewer` so authors only pass a snippet name.
- **Hosting target:** https://docs.shift.software — Windows VPS, IIS. Implications: full server (not a static-only host), so `Docs.API` can be hosted alongside `Docs.Web` on the same box. No GitHub Pages base-href workarounds needed. Deploy pipeline = build artifacts + copy to IIS site root (manual or via a publish script — to be defined).
- **Site is public, no auth gate.** Investigation (2026-04-07) confirmed: `Docs.API` has zero auth code (no `AddAuthentication`, no `[Authorize]`); `Docs.Web` references ShiftIdentity only in `Program.cs` wiring and one demo file (`CustomerInvoiceList.razor`). Identity is leftover scaffolding from the `shift` template, not a real dependency. Action: rip out unused identity wiring during cleanup, OR keep it dormant if a future demo needs it. Decision deferred until we touch `Program.cs`.
- **Live demos: mixed strategy.** Components section keeps clickable live demos against `Docs.API` (already works that way). Guides ship as static code blocks only for v1 — no "try it live" buttons. This means `Docs.API` must stay deployed and seeded for components, but guides have zero deployment dependencies and can be authored offline.
- **Versioning: single-version only for v1.** Docs always reflect the framework version that `Docs.Web` currently references. Old versions are not preserved. When the framework breaks an API, the corresponding doc page is updated in place. Revisit if external consumers start pinning to old framework versions and complaining about doc drift.
- **Component API tables: runtime reflection.** Expand the existing `AutoComponentDocs` component to enumerate `[Parameter]` properties on a given component `Type` and render the table at runtime. To get prose descriptions from `/// <summary>` tags, ship ShiftBlazor's `.xml` doc file as a static asset and parse it client-side. No build-time codegen, no hand-written tables.

## Open Questions

_All initial open questions resolved (2026-04-07). New ones go here as they come up._

## Known Issues

- **Sticky header layout glitches** (noted 2026-04-07 during first review of `WhatIsShiftFramework.razor`). Parked — revisit during the `MainLayout` / nav rewrite in Workstream A.
- **`dotnet new shift` produced a malformed `.sln`** — **fixed at source 2026-04-08, awaiting NuGet publish.** Root cause: a regression in the .NET SDK 10 template engine. When a `.sln` source file's project list didn't fully match `template.json`'s `guids` array, the engine wrote the surviving `Project` entries but failed to write the `Global` / `EndGlobal` block, producing a `.sln` that MSBuild silently treated as empty. **Fix in `ShiftTemplates`** (architecture chosen to leave the team's dev workflow untouched):
  - Added a new `content/Framework Project/StockPlusPlus.dist.sln` — 39-line clean solution containing only the four always-present projects (API, Shared, Data, Web) with a proper Global block. **This is the file users get.**
  - Left the existing `StockPlusPlus.sln` (309 lines, full dev-mode framework references) untouched. **This is what the team uses day-to-day for framework development.**
  - Updated `template.json`: added `StockPlusPlus.sln` to the source-level `exclude` list so the dev .sln never ships, added a `rename` rule mapping `StockPlusPlus.dist.sln` → `StockPlusPlus.sln` so users see a normal solution name, removed the `guids` array (with the rename approach the engine copies the .dist.sln verbatim, sidestepping the bug entirely), removed the now-redundant `(HostIdentifier == vs)` exclude rule.
  - Verified locally end-to-end with `dotnet new shift` from a `dotnet new install` of the local template path: only `<ProjectName>.sln` is produced, contains 4 projects with the full Global block, `dotnet build .sln` compiles cleanly.
  - **Did NOT touch:** the hardcoded `frameworkVersion` / `typeAuthVersion` / `azureFunctionsAspNetCoreAuthorizationVersion` constants in `template.json`. Those are stale on master but the `ShiftTemplates.Builder` tool syncs them from `ShiftFrameworkGlobalSettings.props` during the official release flow, so they self-correct on publish.
  - **Blocked on:** publishing a new `ShiftSoftware.ShiftTemplates` NuGet package containing the fix. Until then, the docs verification script (which fetches from NuGet) still gets the broken published version.

## Suggested Sequencing

A natural order that front-loads the things most likely to invalidate later work:

1. **Lock IA + answer open questions 1, 2, 4, 6** (no point writing pages until we know where they live and what format).
2. **Tooling: extend SnippetGen, pick markdown story, add page-status badge, add search index plumbing.**
3. **Get Started section, end-to-end** (this is the highest-leverage content; ship it first).
4. **Concepts section** (the hardest to write — needs the most editorial care).
5. **Guides** (parallelizable once the template is set).
6. **Components expansion + API generation tooling.**
7. **API reference + versioning + contributor guide.**

Ship the site publicly after step 3 even if everything beyond Get Started is marked Draft. A small, high-quality Get Started + visibly-in-progress everything-else is better than a hidden site waiting to be "complete."

## How to Use This Document

This is the single source of truth for the documentation effort. Update it whenever:
- A workstream item is completed (check the box, add a one-line note if non-obvious).
- A decision is made on an open question (move it from Open Questions into the relevant workstream).
- Scope changes (add/remove items, don't silently drop them).

Do not let this doc go stale. If it stops matching reality, the next person picking up this work will be flying blind — same problem we just had with the mapping plan.
