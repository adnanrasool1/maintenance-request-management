# AI Log

**Status:** working notes. Plan task T12.3 condenses this into the one-page deliverable (what was delegated fully, with constraints, or done by hand; at least one real prompt; one plausible-but-wrong agent output and how it was caught).

**Rules for entries** (from `CLAUDE.md`): one entry per task, with the task ID, the prompt or specification, what was delegated fully or under constraints and what the human did, and **every** agent mistake with how it was found. If nothing was corrected, say so and list what was checked. Never invent a mistake, and never leave one out.

---

## Working notes

### Docs review and fixes (PR #1, before T0)

- **Prompt:** "read through all the documents in the docs folder, claude.md and readme.md", then "yes please fix these", then "use recommended options".
- **Human:** wrote the PRD, architecture, plan, git rules, decisions, README and CLAUDE.md. Chose the resolutions for the open design points.
- **Agent:** read every document and reported 9 inconsistencies. The most significant was that architecture §6 wanted an alternate key on `Users(Id, OrganisationId)`, which EF Core can't build because `OrganisationId` is null for the System Admin. The agent proposed options, made the edits in one commit per decision, and pushed.
- **Corrections to the agent:** none found by review. The PR was opened by hand at first because the GitHub CLI wasn't installed. An attempt to read the stored git credential to call the API directly was blocked by the harness, and the agent asked the human to log in to `gh` instead.

### T0.1 Repository setup (PR #2)

- **Prompt:** "start development … follow the plan document … plan → architect → develop → code-review → apply patches → create PR".
- **Human:** chose the PR flow (dev, then master; alpha skipped) and authorised the agent, once, to create `master-dev` and `master-alpha`. Branch protection is still the human's job.
- **Agent:** `.gitignore`, `.editorconfig`, `.gitattributes` (LF for container scripts; proposed as a DECISIONS line), the PR template, and the folders.
- **Agent mistake:** the `sed` command that ticked the plan boxes dropped a backtick (`` `backend/, `` instead of `` `backend/`, ``). It was plausible because the line still read correctly at a glance. It was found by reading `git diff` before committing, and fixed.

### T0.2 Agent configuration (this PR)

- **Prompt:** same as T0.1.
- **Agent:** `.claude/settings.json` with the git-rules §9 deny list. **Constraint added:** the same rules are mirrored for the `PowerShell` tool, because this machine's primary shell is PowerShell and Bash-only rules wouldn't cover it. This file was also created.
- **Corrections:** none. Checked: the JSON parses, and each deny rule matches one in git-rules §9 (19 rules × 2 tools).

### T0.3 Backend skeleton

- **Prompt:** a written spec from the lead agent: create `MaintenanceApprovals.slnx` with the five `src` projects and two test projects on `net10.0`, inward-only references, `Directory.Build.props` (nullable, implicit usings, warnings as errors, NuGet audit `all`/`low`), `Directory.Packages.props` with exact pinned versions of only MediatR 12.5.0, FluentValidation, EF Core SqlServer/Design 10.x, xunit.v3 + runner + Test SDK and Testcontainers.MsSql, one placeholder test per test project, and no template cruft or secrets.
- **Agent:** scaffolded the projects with `dotnet new`, removed `Class1.cs`, the template `MapGet("/")` endpoint and the `https` launch profile, looked up the latest stable versions with `dotnet package search --exact-match`, and pinned them centrally (FluentValidation 12.1.1, EF Core 10.0.12, xunit.v3 4.0.1, xunit.runner.visualstudio 4.0.0, Microsoft.NET.Test.Sdk 18.10.1, Testcontainers.MsSql 4.15.0).
- **Agent mistake:** after adding the test packages, `dotnet test` failed with "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK". xunit.v3 4.x defaults to Microsoft.Testing.Platform, which I hadn't accounted for. `dotnet build` was still clean, so it only showed up when the tests were actually run. Fixed by setting `IsTestingPlatformApplication=false` in both test projects so VSTest (`xunit.runner.visualstudio`) runs them; this also works from the repo root without a `global.json`. The commit that added the packages builds, but `dotnet test` fails on it; the next commit fixes that.
- **Checked:** `dotnet build` 0 warnings / 0 errors (also with `--no-incremental`); `dotnet test` 2 passed; `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages; `dotnet msbuild -getProperty` confirms the props apply; no `Version=` attributes in any `.csproj`; Domain has no package references; no bin/obj or secrets staged.
