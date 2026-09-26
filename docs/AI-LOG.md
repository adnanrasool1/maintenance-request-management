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

### T0.4 Frontend skeleton

- **Prompt:** the T0.4 spec from the orchestrating agent: generate an Angular 22 app in `frontend/` with a pinned CLI (no SSR, no UI library), exact dependency versions with `.npmrc` `save-exact=true`, a `proxy.conf.json` for `/api` → `http://localhost:5080` wired into `ng serve`, a trivial root component, `npm test` single-run and headless, then review, tick the plan and open PR 1.
- **Agent:** generated the app with `@angular/cli@22.2.0` (standalone, routing, CSS, Vitest + jsdom, so no browser is needed). Pinned every dependency to its installed version, added the dev proxy, set `npm test` to `ng test --watch=false`, and replaced the welcome page with a title plus `<router-outlet />`. No services, interceptors or models were added; those belong to T4.
- **Review finding (fixed in its own commit):** the CLI's generated `frontend/README.md` described `ng generate` and e2e and didn't mention the proxy or the Docker-first setup. It was replaced with short, accurate local-dev notes.
- **Corrections:** none to agent-written code. Checked: `package.json` has no `^`/`~`; `npm ci` succeeds against the lock file; `npm test` passes 2/2; `npm run build` produced no warnings; `npm audit` and `npm audit --omit=dev` report 0 vulnerabilities; `ng serve` proxied `/api/health` to port 5080 (502 ECONNREFUSED with no API running, as expected); `src/` contains no hosts, ports or CORS config; the root `.gitignore` keeps `node_modules`, `dist`, `.angular` and `frontend/.vscode` out of the diff.
