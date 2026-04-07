# Docs.Verify

Anchored verification scripts for the Shift Framework documentation site.

## Purpose

Every command, code snippet, and config example shown to readers on https://docs.shift.software comes from a real source file in this folder — never typed inline into a `.razor` page. The build pipeline (`Docs.SnippetGen`) extracts marked regions from these files and writes them to `Docs.Web/wwwroot/snippets/`, which doc pages then render via `<DocSnippet />`.

This makes it **structurally impossible** for the commands shown in the docs to drift out of sync with reality, because the docs only display what these scripts contain. If a command is wrong here, fix it here — the doc page updates automatically on the next build.

## Layout

- `Scripts/` — bash scripts a real developer would run (install, scaffold, build, etc.).
- `Code/` — C# / Razor reference files used to anchor code snippets shown in Concepts and Guides pages.

## Snippet markers

**Bash (`.sh`):**
```bash
# snippet:Name
echo "this becomes Name.txt"
# endsnippet
```

**C# (`.cs`):**
```csharp
// snippet:Name
public class Foo { }
// endsnippet
```

**Razor (`.razor`):**
```razor
@* snippet:Name *@
<div>...</div>
@* endsnippet *@
```

Snippet names are global. `Docs.SnippetGen` fails the build on duplicates.

## Future: Tier 3

Eventually `Docs.Verify` will grow into an executable project that runs these scripts in a clean container on every PR and asserts they succeed end-to-end. For now, the contract is weaker: scripts in this folder are **manually verified** to work, and the doc only shows what they contain.
