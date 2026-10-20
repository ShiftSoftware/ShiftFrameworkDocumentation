# Contributing to the Shift Framework docs

This is the workflow for adding or updating a page on https://docs.shift.software. It is written for someone who has never touched this repo. For the full reference (helper components, snippet rules, script conventions) see [CLAUDE.md](CLAUDE.md) and [Docs.Verify/README.md](Docs.Verify/README.md).

## 1. Set up

Clone this repo **next to** `ShiftTemplates` (same parent folder) and keep `ShiftTemplates` on `master`:

```
C:\Projects\
  ShiftTemplates\                 <- on master
  ShiftFrameworkDocumentation\    <- your branch
```

`Docs.SnippetGen` reads the current framework version from `../ShiftTemplates/ShiftFrameworkGlobalSettings.props`. If `ShiftTemplates` is on an old or feature branch, every "current version" check is against the wrong number.

Build and run the site:

```bash
dotnet build Docs.Web -p:RunDocsBuildHooks=true
dotnet run --project Docs.API
```

- `-p:RunDocsBuildHooks=true` is needed when `ShiftTemplates` is next to this repo: its props file turns the hooks off by default, and without them snippets and the search index are not regenerated.
- Always run **Docs.API** (it hosts the Blazor app and the demo endpoints) with its normal launch profile (port 5231). `--no-launch-profile` starts it in Production mode and the demos break.
- After pulling new framework code, run with a build (not `--no-build`). If the browser still shows "An unhandled error has occurred", it is holding old cached files: hard-reload, or open `http://127.0.0.1:5231` instead of `localhost`.

## 2. Add the page

Pages are plain `.razor` files (no Markdown) under `Docs.Web/Pages/<Section>/`. Start every page like this:

```razor
@page "/guides/my-topic"

<DocPageMeta Title="My topic"
             Section="Guides"
             Order="50"
             Status="DocPageStatus.Draft"
             Description="One sentence a reader sees under the title and in search."
             Keywords="comma,separated,search,words" />
```

`<DocPageMeta />` is the only place the page's metadata lives. `Docs.SnippetGen` scans it at build time and writes `Docs.Web/wwwroot/search-index.json`, and the side nav and search are built from that file. So:

- **Section** puts the page in the nav. The nav shows, in this order: `Get Started`, `Concepts`, `Guides`, `Components`, `API Reference`. Any other value lands in a group at the end.
- **Order** sorts the page inside its section (lower first). Use multiples of 10.
- Keep each attribute **on one line**, and don't use `<` or `>` in the values (write "the ShiftEntity base class", not `ShiftEntity<T>`). The scanner is a regex; if it can't read the tag, the page is silently missing from the nav and search (the build prints a SnippetGen warning).

For layout use the helpers: `<DocSection>`, `<DocCallout>`, `<DocSnippet>` and `<DocCodeBlock>` (see CLAUDE.md for when to use which).

## 3. Put commands and code in snippets, not in the page

Anything a reader is meant to type or copy should come from a real file, not be typed into the `.razor` page:

1. Put it in a file under `Docs.Verify/` (a script in `Docs.Verify/Scripts/`, or a small C# project that compiles against the framework, like `Docs.Verify/DataLevelAccessV2/`).
2. Mark the region:

   ```bash
   # snippet:MyTopicInstall
   dotnet new install ShiftSoftware.ShiftTemplates
   # endsnippet
   ```

   (`// snippet:Name` / `// endsnippet` in C#, `@* snippet:Name *@` / `@* endsnippet *@` in Razor.)
3. Show it on the page: `<DocSnippet Name="MyTopicInstall" Language="bash" />`.

Rules that bite:

- Snippet names are global. A duplicate fails the build.
- Everything between the markers is shown to readers, comments included. Notes for maintainers go outside the markers.
- Never edit files in `Docs.Web/wwwroot/snippets/`: they are regenerated on every build.
- Use `<DocCodeBlock>` only for illustrative code that isn't meant to be run as-is.

## 4. Verify the page with a Docs.Verify script

A script in `Docs.Verify/Scripts/` does two jobs: it holds the snippets the page shows, and it actually runs them and checks the result. Look at the existing scripts (`install-and-scaffold.sh`, `run-sample.sh`, `add-first-entity.sh`, `data-level-access.sh`) before writing a new one.

- If a script fails, fix the script (and so the snippet), never just the page.
- Run it from an **empty folder**, the way a new developer starts. Then run it again in a different order relative to the other scripts. A script must create what it needs (project, database, migration) and not rely on another script having run first.
- Use `set -uo pipefail`, an `EXIT` trap that stops background processes and drops anything it created, and a unique database name per run.
- Print what each step checked and exit non-zero on any failure, so the run's output can be pasted into the PR as proof.
- Commands that block (like `dotnet run`) go in a function that is never called, so SnippetGen can extract them but the script doesn't hang; the script runs its own background version.
- When you scaffold a test project, don't name it `App`: it clashes with the generated `App` component class and the build fails with hundreds of CS0426 errors.
- If you need a fact the page states that isn't a command (a folder name, a default port, a project reference), check it in the script too.

Pages with no commands (overviews, concepts) are verified by checking each claim against a freshly scaffolded project and the framework source. Write down what you checked in the PR.

## 5. Status and TestedAgainst

| Status | Meaning | Chip |
|---|---|---|
| `DocPageStatus.Draft` | Work in progress, may be wrong. | yellow |
| `DocPageStatus.Reviewed` | Content complete and verified end to end. | blue |
| `DocPageStatus.Stable` | Production-ready, validated against the current framework version. | none |

A page leaves Draft only when its script (or claim check) passes against the current framework version. At the same time set `TestedAgainst` to that version: the value of `ShiftFrameworkVersion` in `ShiftTemplates/ShiftFrameworkGlobalSettings.props`, which is also the NuGet template version (`dotnet new details ShiftSoftware.ShiftTemplates`).

Every build, SnippetGen warns about any non-Draft page with no `TestedAgainst` or an older one ("re-verify the page"). When a new framework release comes out, re-run the page's script, then bump the version.

## 6. Before opening the PR

1. `dotnet build ShiftFrameworkDocumentation.sln -p:RunDocsBuildHooks=true`: 0 errors, and no new SnippetGen warnings for your page.
2. Check `git diff Docs.Web/wwwroot/search-index.json`: only your page's entry should change. If other entries changed, the index on master was stale; mention it in the PR rather than hiding it.
3. Open the page in the running site: it is in the nav under the right section, it shows up in search, the status chip and "Last verified against" line are right, every snippet renders, every link goes somewhere real.
4. Run the page's script one last time from an empty folder and paste the summary into the PR.
5. `git diff --check`, then commit only the files you meant to change.

In the PR description, say which framework version you verified against, how you verified it (script output, or the list of claims checked), and anything you found that looks like a framework or template bug.
