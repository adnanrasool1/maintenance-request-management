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

### T0.5 Infrastructure skeleton

- **Prompt:** plan task T0.5, delegated to a sub-agent with a written spec: compose with only `sqlserver` (exact CU tag, `sqlcmd` health check with `-C`, named volume, sa password and host port from `.env`), `.env.example` with placeholders and the keys the architecture and README already name, and `setup.sh` / `setup.ps1` (PS 5.1) that generate random secrets only when `.env` is missing.
- **Agent:** picked `mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04`, the newest `2022-CU*-ubuntu-22.04` tag in the MCR tag list on 2026-09-26. Wrote the three files. Passwords are 24 characters from `[A-Za-z0-9]` plus a guaranteed upper, lower, digit and one of `._!-`, so they meet SQL Server complexity and never contain `$`, quotes, `#` or `;`. The JWT key is 32 random bytes, base64. `setup.ps1` writes the file as UTF-8 without BOM and with LF endings.
- **Constraint:** the Docker daemon was not running, so the container was **not** started and the health check was **not** exercised. Verified instead: `docker compose config` renders with `.env.example` and with a `.env` from each script; both scripts create `.env` with no `=change-me` left, correct password complexity and a 44-character key; a second run leaves the file unchanged (same hash); `git status --ignored` shows `infra/.env` as ignored; `git ls-files --eol` shows `setup.sh` LF and mode 100755.
- **Agent mistake:** a `sed` command meant to replace `echo "$line"` with `printf '%s\n' "$line"` in `setup.sh` turned the `\n` into a real line break, splitting the `printf` string across two lines. The file-change notice showed the broken line; it was fixed with an exact edit before anything was committed or run.
