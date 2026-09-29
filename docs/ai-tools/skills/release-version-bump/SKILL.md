---
name: release-version-bump
description: Bump the McpGateway.Core package version (e.g. 0.2.0-preview -> 0.2.1-preview). Use whenever asked to "進版"/"bump version"/"release" this repo, so every file that embeds the version string gets updated together and the Jenkins doc version-check doesn't fail.
---

# Release Version Bump

Bumping the version touches more files than the two `.csproj` files. Missing any of them either breaks
the Jenkins `dotnetPkgPipeline` **Verify Documentation** stage (`docs/README.md does not contain version X`)
or leaves stale version strings in shipped docs.

## Checklist — grep first, then edit every hit

Before editing, confirm the full blast radius for this repo:

```
grep -rn "0\.2\.0-preview" --include="*.md" --include="*.csproj"
```

Files that must be updated **together, in the same commit**:

1. `src/McpGateway.Core/McpGateway.Core.csproj` — `<Version>`
2. `src/McpGateway.Analyzers/McpGateway.Analyzers.csproj` — `<Version>` (kept in lockstep with Core per ADR-015 single-package merge)
3. `ReleaseNote.md` — prepend a new `## {version}` section above the previous one, with a real changelog (what changed, issue links)
4. `docs/README.md` — **this is the file the Jenkins `Verify Documentation` stage greps for the version string.** Update:
   - `## 目前版本` bold version line
   - the version history table (add a new row)
   - `## 套件內容（{version}）` heading
   - the `<PackageReference ... Version="..." />` example
5. `src/McpGateway.Core/README.md` — package-level README also embeds the version twice:
   - `- **Version**: {version}` under Package Details
   - the `dotnet add package McpGateway.Core --version {version}` install example

Do **not** bother updating illustrative/example version strings in design docs (e.g. `docs/specs/spec-department-scaffold.md`) — those are placeholder examples, not live references, unless the user explicitly asks.

## Commit discipline

- All of the above land in **one single commit** (e.g. `chore(release): 版本 bump {version}`) — the user has explicitly asked for this pattern before. Stage everything, then commit once; don't split csproj bumps from doc updates across commits.
- If a version-bump commit was already pushed and a doc file was missed, fix it with `git commit --amend` + `git push --force-with-lease`, not a new follow-up commit — but only do this if the commit hasn't been built on by others (check `git log`/ask the user first if unsure, since force-push rewrites shared `master` history).

## Verification

After editing, re-run the grep for the **old** version string — it should return zero hits outside of intentionally-untouched historical/example references:

```
grep -rn "{old-version}" --include="*.md" --include="*.csproj"
```
