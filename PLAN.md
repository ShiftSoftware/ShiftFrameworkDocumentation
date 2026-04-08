## Where to pick up next session (last updated 2026-04-08)

**Headline progress:** 11 doc pages drafted across Get Started (6/6) and Concepts (4/10). All verification tooling (Tiers 1, 2, 2.5) is built and proven on three working scripts. Two upstream `ShiftTemplates` bugs were found and fixed locally, awaiting NuGet publish.

**To start next session:**
1. Read this section + the latest "Done" entries under Workstreams C and D below to see what's already shipped.
2. Continue **Workstream D — Concepts**, picking up at **TypeAuth** (next unchecked item under "Content — Concepts"). The natural research path: read `TypeAuth.Core` source AND a real production action tree from `Toyota-Iraq/ActionTrees`. Same workflow as the previous Concepts pages — read source, find production usage via `.shift/CLAUDE.md`, write the page, build, mark Draft.
3. Refresh `Docs.API` and visually skim any Concepts pages already drafted that haven't been eyeballed yet. They're all marked Draft (yellow dot in the side nav) — bump to Reviewed if they read well.
4. Once `ShiftTemplates` is published with the upstream fixes (see Known Issues), re-run `Docs.Verify/Scripts/install-and-scaffold.sh` against the new NuGet version. The Installation page is currently marked Reviewed against the fix as it works locally; publish closes that loop.

**Outstanding blockers:**
- `ShiftTemplates` NuGet publish (blocks: full re-verification of Installation page against the published version, not just the local working tree)
- No CI for verification scripts yet (Tier 3 deferred). The existing scripts are run-on-demand only.

**No in-flight work** — the session paused cleanly. No half-finished pages, no uncommitted changes from a partial refactor.

**Things worth knowing if you're a fresh session reading this:**
- Run `Docs.API` to launch the docs site locally — never `Docs.Web` directly. Reason in `CLAUDE.md`.
- Never edit page source to fix verification failures. Fix the script in `Docs.Verify/Scripts/`, the doc updates automatically. Reason and full workflow in `CLAUDE.md`.
- For C# code samples that contain `<` (generics, comparisons, HTML), use `<DocCodeBlock Code="@(@""...""))" />` not `<pre><code>` — Razor's parser chokes on `<` even inside `<pre>`. Helper exists for exactly this. Reason in `CLAUDE.md`.
- For Concepts pages: ground in actual framework source AND real production usage from `Toyota-Iraq/...` or `Toyota-Centralasia/...`. The internal index at `C:\repos\ShiftSoftware\.shift\` lists which client repos exist and what they contain. The DTO page would have shipped wrong (called the pattern a "triad" instead of a "pair") if I'd worked from the StockPlusPlus sample alone — production code revealed the actual pattern.

---

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
- **Tier 2 — Anchored commands and code.** Every command/code snippet shown to readers comes from `Docs.Verify/` or another scanned source root, never typed inline. Doc pages reference snippets via `<DocSnippet Name="..." />`. Structurally prevents drift on the code side. ✅ Built. SnippetGen scans `Docs.Verify/` and supports `.sh` / `.bash` files with `# snippet:Name` markers.
- **Tier 2.5 — Run-on-demand verification scripts.** ✅ Built 2026-04-08. The same anchored scripts are now complete enough to actually execute end-to-end. I (Claude) run them whenever I write or update a page that depends on them. Failures get fixed at the script source before the page is bumped to Reviewed. Pattern: a script lives at `Docs.Verify/Scripts/<name>.sh`, the doc page references regions via `<DocSnippet>`, and the script also contains the verification harness (background process management, port probing, cleanup) outside the snippet markers. Authoring rule documented in `CLAUDE.md`: "snippets the user runs" and "verification harness that proves they work" can coexist in the same script using a never-called `docs_only_anchors` function for snippets that would block (e.g. `dotnet run`). Working examples: `install-and-scaffold.sh`, `run-sample.sh`.
- **Tier 3 — Executable doctests in CI** (deferred). The Tier 2.5 scripts are designed to be re-runnable in CI as-is, but currently rely on Windows-only LocalDB. Adding Linux CI would need either Docker mssql or a `--databaseProvider sqlite` template parameter. Revisit when we have 3-4 verified Tier 2.5 scripts and want to lock the gains in.
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
- [x] **Done 2026-04-08.** Template parameters — `Pages/GetStarted/TemplateParameters.razor`. Status: Draft. Covers required params (`--includeSampleApp`, `--shiftIdentityHostingType`), optional params (`--addFunctions`, `--addTest`, `--externalShiftIdentityApi`, `--externalShiftIdentityFrontEnd`), auto-generated values (user secret IDs, dev ports, project GUIDs), and three common-combination recipes. Reuses the `ScaffoldFirstProject` snippet from `Docs.Verify` so the basic example stays in lock-step with verification.
- [x] **Done 2026-04-08.** Project tour — `Pages/GetStarted/ProjectTour.razor`. Status: Draft. Walks through Shared / Data / API / Web (and the optional Functions / Test), explains the dependency direction, and traces a single end-to-end request through all four layers so the rhythm is concrete. Built from inspection of the actual template content; needs verification that the project listings match what users get on a fresh scaffold.
- [x] **Done + Verified 2026-04-08.** Run the generated sample — `Pages/GetStarted/RunTheSample.razor`. Status: Draft (will bump to Reviewed once it's been refreshed in your browser and you confirm the prose lands right). New `Docs.Verify/Scripts/run-sample.sh` brings the scaffolded app up end-to-end against LocalDB and was run successfully ("API responded on attempt 2"). Verification surfaced four real friction points that are now in the doc page: (1) the template's default `localhost\sqlexpress` connection string doesn't exist on most machines, (2) the empty Migrations folder requires `dotnet ef migrations add InitialCreate` as a first step, (3) `dotnet ef` requires `--context DB` because the project defines two DbContexts, (4) `appsettings.Development.json` is the only config file (no base appsettings.json) so `ASPNETCORE_ENVIRONMENT=Development` must be set. Page also includes the seeded admin credentials (`SuperUser` / `OneTwo`) and a Common Errors section listing the exact error messages a user will hit if they skip a step.
- [x] **Done + Verified 2026-04-08.** Your first entity — `Pages/GetStarted/YourFirstEntity.razor`. Status: Draft. Walks through scaffolding a new entity with `dotnet new shiftentity -n Vehicle --solution MyFirstShiftApp`, lists the eight generated files, explains the DbContext partial-class registration trick, then covers customizing the entity, adding a migration, applying it, and running. New `Docs.Verify/Scripts/add-first-entity.sh` runs the scaffold and builds the API project to confirm everything compiles. Verification surfaced **three real bugs** in the `shiftentity` item template that all needed upstream fixes (see Known Issues).

### D. Content — Concepts
- [x] **Done 2026-04-08.** ShiftEntity model — `Pages/Concepts/ShiftEntityModel.razor`. Status: Draft. Covers the base class, the `long ID` convention (with a warning about brownfield projects), the four audit fields and how the repository populates them automatically, soft delete + the `IsDeleted` query filter, the `ReloadAfterSave` coordination hook, and the `[TemporalShiftEntity]` attribute for SQL Server temporal history. Grounded in actually-read source from `ShiftEntity.Core/ShiftEntity.cs`. Also surfaced and fixed a SnippetGen regex bug: the previous `[^/>]*` pattern silently dropped pages whose `<DocPageMeta>` description contained literal `<` or `>` characters (e.g. `ShiftEntity<T>`). New pattern is non-greedy `(.*?)` and the scanner now warns loudly when a file mentions `DocPageMeta` but can't be parsed.
- [x] **Done 2026-04-08.** The DTO pattern — `Pages/Concepts/DtoPattern.razor`. Status: Draft. **Important correction**: previously called "DTO triad," but reading actual framework + production code revealed it's a **pair**, not a triad. Two base classes: `ShiftEntityViewAndUpsertDTO` (read AND write merged into one) and `ShiftEntityListDTO`. Page covers the merge rationale, the base classes, the hashid string ID encoding (and why), and the most non-obvious finding: the View vs List DTOs differ specifically in how they shape foreign keys (`ShiftEntitySelectDTO` for forms vs plain `string? FooID` for grid columns). Grounded in `ShiftEntity.Model/Dtos/*.cs` source AND a real production example from `Toyota-Iraq/Services/Services.Shared/DTOs/AutolineBrandMapping/`. Also fixed the previous Concepts page (ShiftEntityModel) which had referred to "View / List / Upsert triad" — corrected to ViewAndUpsert / List pair. Design observation about why this pattern feels unconventional (and why it might be the right call anyway) recorded under "Design Observations" in the plan.
- [x] **Done 2026-04-08.** Repositories and the request lifecycle — `Pages/Concepts/Repositories.razor`. Status: Draft. Covers where business logic lives (repositories, not controllers), the standard 5-line controller pattern, the full 10-step lifecycle of a single-record write (auth → UpsertAsync → MapToEntity → audit fields → BeforeSave → SaveChanges → ReloadAfterSave → AfterSave → ViewAsync → response), the IQueryable-based list path that translates OData query options into a single SELECT, and the BeforeSave/AfterSave hook interfaces (with the warning that AfterSave changes the save path to use an explicit transaction). Grounded in `ShiftEntity.EFCore/ShiftRepository.cs` (508 lines) and `ShiftEntity.Web/ShiftEntitySecureControllerAsync.cs`. Also added a new `<DocCodeBlock>` helper component to handle inline C# samples with generics/comparisons/HTML — Razor parser was choking on `<` characters even inside `<pre><code>`, so the helper takes the code as a string parameter. Documented the helper and the rule in `CLAUDE.md`.
- [x] **Done 2026-04-08.** The mapping abstraction — `Pages/Concepts/MappingAbstraction.razor`. Status: Draft. Covers why the abstraction exists (decoupling from AutoMapper, the implicit-flattening risk), the four-method `IShiftEntityMapper<TEntity, TListDTO, TViewDTO>` interface, the four supported strategies (AutoMapper / Manual / Mapperly / Mapster) with honest pros/cons for each, the `appsettings.MappingStrategy` switch, the helper extension methods (`MapBaseFields`, `ToSelectDTO`, `ToForeignKey`, `ToShiftFiles`, `ShallowCopyTo`) that make manual mapping practical, and a practical recommendation: start with AutoMapper, switch to Manual when you hit your first runtime mapping bug. Grounded in `ShiftEntity.Core/IShiftEntityMapper.cs` and `StockPlusPlus.Data/Mappers/ProductMapper.cs`.
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

## Design Observations (worth revisiting)

While writing concept pages I'll flag framework patterns that surprised me when compared to mainstream .NET / API conventions. These are not bugs and not criticisms — just notes for a future design-review pass to decide whether the unusual choice is load-bearing or whether mainstream alternatives would serve better.

### Docs internal: DocSnippet vs DocCodeBlock duplication — noted 2026-04-08

**What we have:** Two separate components for rendering code blocks. `<DocSnippet Name="..." />` handles anchored code from real source files (via `DocFileHelper.GetSnippetByName` → `CodeViewer`), with copy + download buttons. `<DocCodeBlock Code="..." />` handles inline illustrative code passed as a string parameter, with copy only. They share ~80% of their concerns: same Prism highlighting, same dark/light theme handling, same copy-to-clipboard, same min-height + button positioning logic.

**Why it's inconsistent:** A reader looking at two adjacent code blocks on the same page can't tell which is "anchored" and which is "illustrative." The download button appearing on one but not the other looks arbitrary. The two components also evolved separately — when I fixed the single-line button alignment in `CodeViewer`, the same bug existed in `DocCodeBlock` and I had to remember to fix it in two places. Future bugs will keep diverging unless they share an inner renderer.

**Cleanup direction:** Refactor both into thin wrappers around a shared `<DocCodeRenderer>` inner component that owns the rendering, theming, copy button, and Prism interop. `DocSnippet` then becomes a wrapper that resolves a snippet name into a string + filename and passes them to the renderer; `DocCodeBlock` becomes a wrapper that just passes the string through. Download button is shown only when the renderer has a filename. Not urgent — both work today — but worth doing before the divergence gets worse.



### DTO pattern: ViewAndUpsert + List (rather than separate Create/Read/Update DTOs) — noted 2026-04-08

**What Shift does:** Each entity has two DTO classes — one `: ShiftEntityViewAndUpsertDTO` (used for both reading a single record AND for inserting/updating it) and one `: ShiftEntityListDTO` (used for list/grid projections). The View+Upsert merge means the same shape carries both directions on a single-record endpoint. The split between View+Upsert and List exists primarily so foreign keys can be different shapes in each: `ShiftEntitySelectDTO` (id + display label) for forms, plain `string? FooID` for grid columns.

**Why it might feel unconventional:**
1. **CQRS-influenced styles separate read from write.** Most "modern" .NET API tutorials and microservices guides preach a strict triad: separate `CreateFooRequest`, `UpdateFooRequest`, and `FooResponse` (or similar). The reasoning is that create-time fields (e.g. initial password), update-time fields (e.g. last-modified), and read-time fields (e.g. computed properties) are genuinely different.
2. **Hashid string IDs on the wire instead of native long.** Most APIs serialize entity IDs as plain numbers or GUIDs. Shift encodes `long ID` as opaque hashid strings (`"abc"` instead of `123`) to prevent ID enumeration in admin URLs. This is a security trade-off but adds friction (clients can't do math on IDs, can't sort by them as numbers, can't easily generate URLs from server-side knowledge of an ID).
3. **The "two DTOs differ in foreign-key shape" insight is non-obvious.** A new developer reading the framework for the first time will see "ProductDTO" and "ProductListDTO" and assume the difference is "list shows fewer fields." The actual difference (`ShiftEntitySelectDTO Brand` vs `string? BrandID`) is a structural choice that took reading three production DTO files to spot.

**Why it might be the right call for line-of-business apps:**
1. The same form is used to view AND edit a record in 95% of admin UIs. Having one DTO matching that form is cleaner than two near-identical classes.
2. List vs form genuinely need different shapes — lists need flat columns for grid binding, forms need rich nested objects for autocomplete pickers. The two-DTO split fits this naturally.
3. Hashids prevent the "guess the next ID" attack class on admin URLs without the team having to think about it. This is a real risk in line-of-business apps.

**To revisit:** is the merged ViewAndUpsert pulling its weight, or would a strict triad (Create/Update/Read) be clearer for new developers? Are hashids worth the friction? These are framework-design questions, not docs questions, but the docs work is what surfaced them.

## Known Issues

- **Sticky header layout glitches** (noted 2026-04-07 during first review of `WhatIsShiftFramework.razor`). Parked — revisit during the `MainLayout` / nav rewrite in Workstream A.
- **`dotnet new shiftentity` produces non-compiling code** — **fixed at source 2026-04-08, awaiting NuGet publish.** Verification of the "Your first entity" page surfaced three separate bugs in `content/ShiftEntity/.template.config/template.json`:
  1. **Missing `includeItemTemplateContent` symbol.** The source `ProductBrandForm.razor` wraps its `@code` block in `@*#if (includeItemTemplateContent)*@ ... @*#endif*@` template-engine conditionals. The `shift` parent template defines this symbol so the conditional content survives instantiation, but the `shiftentity` item template did not. Result: the entire `@code` block was stripped from generated forms, leaving Razor markup that referenced undefined identifiers like `BrandItem` and `Key`. **Fix**: added `includeItemTemplateContent` as a `generated`/`constant` symbol with `value: "true"`.
  2. **`typeAuthAction.replaces` matched the wrong string.** The config tried to replace `StockPlusPlus.Shared.ActionTrees.StockPlusPlusActionTree.ProductBrand` (fully qualified) with `null`, but the source `.cs` controller actually uses just `StockPlusPlusActionTree.ProductBrand` (bare, with a `using` directive). The replacement never fired for `.cs` files, so generated controllers referenced a non-existent action tree node. **Fix**: split into two symbols — `typeAuthActionFullyQualified` for `.razor` files (which use the long form) and `typeAuthActionBare` for `.cs` files (which use the short form).
  3. **Generated form references brand-specific properties.** The `shiftentity` template duplicates the rich `ProductBrand` example as the source for any new entity. Properties like `BrandName`, `<ProductList>`, `Name`, `Team` get carried over and won't compile against an empty entity. **Not fixed in this pass** — the doc page covers this honestly by telling users that the scaffolded form is a starting point they need to customize for their domain. A proper fix would mean re-templating the item to produce a minimal generic starter, which is a larger redesign.
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
