# CLAUDE.md

These are instructions for AI agents working in this repository. Read this file fully before every task.

## Project in one paragraph

This is a multi-tenant backend, with a minimal UI, for maintenance requests and cost approval. Client **organisations** own **sites** and **users**. Users raise **maintenance requests**, and requests at or above the organisation's cost threshold need an **Approver**. Every state change is audited, and a spend report shows totals per site. Data must be **strictly isolated between organisations**. This is a time-boxed evaluation (4–6 hours), and **over-engineering counts against us.**

## Read these before starting a task

| Document | Read it when |
|---|---|
| [docs/PRD.md](docs/PRD.md) | Always. This is **what** to build. Requirement IDs (FR-x) go in PR descriptions. |
| [docs/architecture.md](docs/architecture.md) | Always. This is **how** to build it: layers, patterns, data model, security. |
| [docs/plan.md](docs/plan.md) | To find your task ID, its lane, dependencies, acceptance criteria and the Definition of Done. |
| [docs/git-rules.md](docs/git-rules.md) | Before any git command or PR. |
| [docs/api-contract.md](docs/api-contract.md) | For any endpoint or frontend work. The contract is frozen, so changes need approval from all lanes. |
| [docs/task-brief.md](docs/task-brief.md) | For the original client requirements, if the PRD seems unclear. |
| [docs/DECISIONS.md](docs/DECISIONS.md) | Before proposing an alternative to an existing decision. |
| [docs/AI-LOG.md](docs/AI-LOG.md) | Where to record prompts and agent mistakes (see "Logging" below). |
| [README.md](README.md) | Before changing anything about setup, ports, configuration or run steps. |

If documents conflict, the PRD wins on behaviour and the architecture document wins on implementation. Report the conflict; don't choose one silently.

## Scope

- Build **only** what the PRD and your current plan task describe.
- **Check the task's dependencies in the plan before starting.** If a task it depends on isn't merged yet (for example, T2 needs T1.1), stop and say so.
- If you think something extra is needed, **stop and ask**. Don't build it. Suggest Future Scope items in the PR description; don't edit the PRD yourself.
- Items under "Open decisions" in the plan (for example, D-1, the demo-data seed) are **not approved**. Don't implement them.
- Don't refactor, rename or "improve" code outside the files your task touches.

## Stack (fixed; don't substitute)

- **Backend:** .NET 10, ASP.NET Core Minimal APIs, Clean Architecture (`Mra.Domain` ← `Mra.Application` ← `Mra.Infrastructure` / `Mra.Api`), and `Mra.DbMigrator`.
- **CQRS:** **MediatR 12.5.0, pinned. Never upgrade it**, because version 13 and later are commercially licensed.
- **Validation:** FluentValidation, run through `ValidationBehavior`.
- **Mapping:** **manual only**, using `ToDto()` extension methods or LINQ `Select` projections. **Never add AutoMapper, Mapster or any other mapping library.**
- **Data:** EF Core 10 with SQL Server. Migrations are code-first.
- **Frontend:** Angular 22, standalone components and signals, with no UI library.
- **Tests:** xUnit and Testcontainers (real SQL Server). **Never use the EF InMemory provider**, and never use FluentAssertions.
- **Packages:** the packages named in architecture §1 are pre-approved. **Any other NuGet or npm package needs human approval.** NuGet versions live only in `backend/Directory.Packages.props`.
- **Versions are always pinned:** packages and Docker images. Never use `latest` or floating versions.

## Non-negotiable rules

### Tenant isolation (architecture §7)

1. The organisation ID comes **only** from `ICurrentUser`, which reads the JWT claims. Never take it from a route, query string or request body, and never add an organisation ID to an API route or DTO.
2. Every tenant-owned entity has `OrganisationId` and a **global query filter**.
3. `IgnoreQueryFilters()` is allowed **only** in the login handler and the System Admin handlers. Anywhere else is a bug.
4. When a requested ID isn't found, or belongs to another tenant, throw `NotFoundException` (→ **404**). Never return 403 for another tenant's data, because that would reveal the ID exists.
5. Look up referenced entities (for example, the site for a new request) through the **filtered** query, so another tenant's ID results in 404.
6. Don't weaken the `SaveChangesInterceptor` guards or the composite foreign keys.

### Code structure (architecture §4)

- Organise code **by feature, one folder per use case**: command or query, handler, validator.
- Endpoints stay thin: they send a MediatR request and return the result. **No business logic or database access in endpoints.**
- Handlers signal errors by **throwing the defined exceptions** (`NotFoundException`, `ForbiddenException`, `ValidationException` from Application; `InvalidTransitionException`, `SelfApprovalException` from Domain). The central exception handler turns them into ProblemDetails, so don't build error responses in handlers or endpoints.
- Don't catch a `DbUpdateConcurrencyException` in order to retry. Let it reach the handler, which returns **409**.

### Domain and workflow (architecture §4.1)

- A request's status changes **only** through `MaintenanceRequest` methods. There are no public setters for `Status`.
- Legal transitions are defined **only** in the domain's transition table. Don't duplicate them in handlers or in the UI.
- Every transition appends an `AuditEntry` inside the aggregate.

### Persistence

- Each command handler calls `SaveChangesAsync()` **exactly once**, at the end. Don't use explicit transactions in handlers.
- Queries use `AsNoTracking()` and project to DTOs. Don't load whole entities just to return them.
- There are no repository or unit-of-work classes. Use `IAppDbContext` directly.
- Store money as `decimal(18,2)` and time in UTC through `TimeProvider`. Never call `DateTime.Now` or `DateTime.UtcNow`.
- **Migrations are applied only by `Mra.DbMigrator`.** The API must never call `Database.Migrate()` or create the schema at startup.
- Each migration is its own commit, and **a human reads the generated SQL before the PR is raised.** Never edit a migration that has already been merged; add a new one instead.

### Audit integrity (architecture §8)

- Nothing may update or delete an `AuditEntry`, whether in code, in a migration, or through raw SQL.
- The API runs as `mra_app`, which has `DENY UPDATE, DELETE` on `AuditEntries`. Never change the API to use the owner (`sa`) login, and never grant `mra_app` more rights.

### Security

- **Every endpoint has an explicit authorization policy.** A fallback policy requires authentication. The only anonymous endpoint is login. Never add `AllowAnonymous` anywhere else.
- Enforce authorisation on the server with endpoint policies, handler ownership checks and domain invariants. Hiding something in the UI is not authorisation.
- Validate all input at the API boundary with FluentValidation.
- Login returns the **same error** whether the email or the password is wrong.
- **Never commit secrets or `.env` files.** Configuration comes from `infra/.env`, which is git-ignored. Only `infra/.env.example`, with placeholders, is committed. Required secrets (JWT key, connection strings) make startup fail when missing. Never add a hard-coded default for them.
- OpenAPI and Scalar are enabled in Development only.

### Frontend and infrastructure

- The frontend calls the API through **relative `/api` paths only**. Never hard-code API hosts or ports.
- **Don't add CORS configuration.** nginx serves the app and the API from a single origin, so CORS isn't needed.
- Store the token in `sessionStorage` through `AuthService` only.
- **Docker is the only prerequisite for running the project.** Don't add steps that need anything else installed, and keep the setup under 15 minutes.
- Host ports and all configuration come from `infra/.env`.

## Keeping files in sync

When your change affects one of these, update it in the **same PR**:

| If you change… | Also update |
|---|---|
| A configuration key or environment variable | `infra/.env.example` and `infra/setup.sh` / `setup.ps1` |
| Ports, URLs, run steps, prerequisites | [README.md](README.md) |
| A significant technical choice, or an assumption you made to resolve an ambiguity | Propose a line for [docs/DECISIONS.md](docs/DECISIONS.md) in the PR |

## Git (full rules in [docs/git-rules.md](docs/git-rules.md))

**Branches and PRs**

- Create base branches **from `master` only**, named `{type}/{YYYY-MM-DD}/{name}`. Do all real work on the base branch.
- When the feature is complete, create the `-dev` copy and open **PR 1 → `master-dev`**.
- Create the `-alpha` copy and open **PR 2 → `master-alpha`**, or open **PR 3 → `master`**, **only when the human tells you** the previous stage is verified.
- If the base branch changes after a copy was made, merge the base branch **into** the `-dev` / `-alpha` copy.
- PR titles use Conventional Commits, `[dev]` or `[alpha]` for PR 1 and PR 2, and the task ID at the end. For example: `[dev] feat(api): approve and reject endpoints (T7.4)`.
- Keep a PR small enough to review in about 15 minutes.

**Commits**

- Use Conventional Commits with the scopes `domain`, `app`, `infra`, `api`, `db`, `web`, `docker`, `docs`, `tests`.
- Make one logical change per commit, and the solution must build at every commit.
- Keep the `Co-Authored-By` trailer.

**You MUST NOT**

- approve, merge or close PRs
- push to `master`, `master-alpha` or `master-dev`
- create a branch from `master-dev` or `master-alpha`
- merge `master-dev` or `master-alpha` into a base branch or `master` (merging them into a `-dev` / `-alpha` copy to resolve a conflict needs the human's explicit permission each time)
- force-push, rebase, `commit --amend` or `reset --hard` anything that has been pushed
- delete remote branches or create tags
- skip hooks with `--no-verify`, or change repository or branch-protection settings

**If a command is blocked** by `.claude/settings.json`, that is intentional. Don't retry it in another form; ask the human.

**If you notice a secret was committed:** stop and tell the human. Don't try to rewrite history.

## Workflow for every task

1. **Plan.** State the task ID, confirm its dependencies are merged, and list the files you'll touch and your approach. Wait for confirmation on anything non-trivial.
2. **Implement** in small commits on the base branch. Add tests when the task touches a risk area from architecture §13: tenant isolation, authorisation, state transitions, the spend report, or audit integrity.
3. **Verify** before each commit, for the parts you changed:
   ```bash
   dotnet build backend/MaintenanceApprovals.slnx
   dotnet test  backend/MaintenanceApprovals.slnx
   cd frontend && npm test && npm run build
   ```
   Check `git diff --staged` for secrets and unrelated changes. Run `git branch --show-current` and confirm you are not on `master`, `master-alpha` or `master-dev`.
4. **Report** honestly. Say what you verified and how, and what you did **not** verify. Never say tests pass without running them.
5. **Update the plan.** In [docs/plan.md](docs/plan.md), change `[ ]` to `[X]` for each sub-task you completed, and commit the change on your base branch as part of the same PR.
   - Mark a sub-task only when it meets the plan's Definition of Done, apart from the human merge, which the PR itself covers.
   - Mark the parent task `[X]` only when **all** of its sub-tasks are `[X]`.
   - Change only the lines for **your own task ID**. Never tick, untick or reword anything else in the plan.
   - If you only partly completed a sub-task, leave it `[ ]` and say what's missing in the PR description.
6. **Log** the task in [docs/AI-LOG.md](docs/AI-LOG.md) (see below).
7. **Open a PR** using the template, including the "AI involvement" section. A human merges it. The ticks count as done only once the PR is merged.

## Useful commands

```bash
# Add a migration (then stop, so a human can read the generated SQL)
dotnet ef migrations add <DescriptiveName> \
  --project backend/src/Mra.Infrastructure \
  --startup-project backend/src/Mra.DbMigrator

# Run the full stack
cd infra && ./setup.sh && docker compose up --build
```

## Logging

After each task, add a short entry to the working-notes section of [docs/AI-LOG.md](docs/AI-LOG.md). Include:

- the task ID
- the prompt or specification you were given
- what you did fully, what you did under constraints, and what the human did by hand
- **anything you produced that was wrong or had to be corrected, and how it was found**

If nothing was corrected, say so and list what was checked to confirm it. **Never invent a mistake to fill the entry**, and never leave one out.

## Stop and ask when

- a task needs a new package, a new endpoint, a new table, or a change to the API contract
- a task depends on work that isn't merged yet
- a rule in this file seems to block the task, or a command is blocked
- you'd need `IgnoreQueryFilters()` outside the allowed places, raw SQL, or a change to the audit table or database permissions
- a document is unclear or contradicts another
