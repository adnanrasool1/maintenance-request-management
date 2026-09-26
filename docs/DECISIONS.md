# Decisions

This page records the significant choices, what was rejected, and the trade-offs. Details are in [architecture.md](./architecture.md), and the full list of assumptions is in [PRD §11](./PRD.md#11-assumptions-ambiguities-resolved).
**Source:** **S** = decided by the stakeholder (project owner). **E** = engineering recommendation, accepted by the stakeholder.

---

## Guiding principle: YAGNI ("You Aren't Gonna Need It")

We build only what the brief and PRD require **today**. A feature, abstraction or layer is added only when a current requirement needs it, not because it might be useful later. Every item left out is recorded in [§5](#5-yagni--deliberately-not-built) with the **trigger** that would justify building it. That way "not built" is a decision, not an oversight.

YAGNI never applies to security or correctness. Tenant isolation, server-side authorisation and audit integrity are protected by several layers, because the brief treats them as compliance requirements.

---

## 1. Stack and structure

| Decision | Src | Rejected | Trade-off |
|---|---|---|---|
| **.NET 10**, one service, **Clean Architecture** (Domain / Application / Infrastructure / Api) | S | Several microservices; a single-project API | Four projects is more ceremony than one, but the dependency direction keeps domain rules separate from EF Core and HTTP. |
| **CQRS with MediatR 12.5.0, pinned** | S | MediatR 13+ (commercial); a separate read store | We stay on a version that gets no new features. Reads and writes share one database, which is enough at this size. |
| **Manual mapping**, no library | S | AutoMapper 14 (unpatched high-severity DoS, GHSA-rvv3-g6hj-g44x); Mapperly | A little more code, but mappings are checked at compile time and the dependency risk is zero. |
| **SQL Server** (Developer edition, in a container) | S | PostgreSQL, SQLite | Strong fit with .NET, and we rely on its `DENY` permissions, `rowversion` and composite FKs. The image is large and amd64-only, so it needs Rosetta on Apple Silicon. |
| **Angular (latest, v22)**, no UI library | S | Server-rendered pages; component libraries | A second toolchain to maintain, but no styling work is needed. |
| **Docker is the only prerequisite** | S | Local .NET SDK + Node + SQL Server | The first build is slower, but it's the only reliable way to set up a clean machine in under 15 minutes. |
| Folders `backend/`, `frontend/`, `infra/`, `docs/`. Brief deliverables live in `docs/`; README and CLAUDE.md stay at the root. | S | Everything at the root | Reviewers find everything in `docs/` through the README's links. |

## 2. Data, security and architecture

| Decision | Src | Rejected | Trade-off |
|---|---|---|---|
| **No repository or unit-of-work layer.** Handlers use `IAppDbContext`, and EF Core is the unit of work. | E | Generic or entity-specific repositories | Handlers depend on EF abstractions, but query projection stays efficient and there is no layer that only passes calls through. |
| **One `SaveChanges` per command**, with no transaction pipeline behaviour | E | `TransactionBehavior` | Commands must be designed to finish with a single save. |
| **Tenant isolation in the data-access layer:** org ID from the JWT, global query filters, a write interceptor, and composite FKs | E | Per-endpoint checks; SQL Server Row-Level Security; database or schema per tenant | Less protection than RLS if an attacker got direct database access. RLS is listed as future hardening. |
| **Composite FK for request → site only**; plain FK for the raiser | E | Composite FK to `Users(Id, OrganisationId)` as well | The raiser always comes from the JWT, and the System Admin's null `OrganisationId` can't be part of an EF Core alternate key. |
| **System-wide email uniqueness is enforced by the unique index**; Infrastructure turns the violation into a 400 validation error (API contract D-2) | S | Allowing `IgnoreQueryFilters()` in the Tenant Admin create-user handler; emails unique per organisation | The 400 reveals that an email exists in some organisation, which system-wide uniqueness makes unavoidable. The `IgnoreQueryFilters()` allow-list stays at login and System Admin. |
| **Another tenant's ID returns 404, not 403** | E | 403 | Clients can't tell "forbidden" from "missing", which is intended. |
| **Two database logins:** the migrator uses the owner login; the API uses `mra_app` with `DENY UPDATE, DELETE` on the audit table | E | A single `sa` login | More setup, but the audit trail is protected even from bugs in the application. |
| **JWT in `sessionStorage`**, single origin through nginx, CSP, 60-minute lifetime | E | HttpOnly cookies | Weaker against XSS than cookies. The risk is reduced by the CSP and the short lifetime. |
| **Local compose runs the API as `Development`** so reviewers can do the admin setup through Scalar | E | A separate "enable API docs" setting; curl examples only | Simple and uses the standard switch, but the local container isn't production-configured. Production never enables Scalar. |
| **OpenAPI document and Scalar UI are anonymous in Development only** — the one exception to "only login is anonymous" | S | Keeping them behind the fallback policy (they 401 in a browser, so the README admin flow breaks); curl-only admin setup | The API description is readable without a token on a developer machine. Every API call from Scalar still needs a bearer token, and neither endpoint exists outside Development. |
| **Tests against real SQL Server** (Testcontainers) on the risk areas only | E | EF InMemory; aiming for coverage | Slower tests, but they check exactly what InMemory would ignore: filters, FKs and permissions. |

## 3. Product decisions

| Decision | Src |
|---|---|
| Roles: seeded **System Admin** → creates organisations and **Tenant Admins** → Tenant Admins create **Requesters, Approvers and sites** | S |
| **The approval threshold** is stored per organisation in the database, and only the Tenant Admin can change it | S |
| **Sites** are created by the Tenant Admin and shown to org users (for the site dropdown) | S |
| **Overrun:** if the actual cost is more than was authorised (auto-approved: at or above the threshold it was approved under; manually approved: above the approved estimate), the request still completes, but it is flagged and audited. Comparing manual approvals to the threshold would flag almost all of them, since their estimate was already at or above it. Blocking completion can't undo money already spent. | E |
| A cost **equal** to the threshold requires approval | E |
| Costs are capped at **1,000,000.00**: generous for maintenance work, and it catches typos and absurd values | E |
| The approval threshold is capped at **1,000,000.00** as well; a higher value would auto-approve every valid request anyway (API contract D-4) | S |
| Input limits the PRD leaves open: organisation and site names 1–200 characters, approve/reject comments ≤ 2,000, passwords 8–128, emails ≤ 256 (API contract D-5) | S |

## 4. Process and ways of working

| Decision | Src |
|---|---|
| Strict scope: nothing beyond the brief. Anything essential is **asked for, not built**. | S |
| Branches `master` / `master-alpha` / `master-dev`; work branches **always from `master`**, with `-dev` and `-alpha` copies for each environment | S |
| **AI agents cannot approve or merge PRs.** Enforced by `CLAUDE.md`, Claude Code deny rules, and branch protection. | S |
| Merge commits only, **no squash**, no force-push. This keeps the history the brief asks to see. | E |
| The plan has parallel lanes with `[ ]` / `[X]` tracking. Agents tick their own sub-tasks within the PR. | S |

---

## 5. YAGNI — deliberately not built

| Not built | Build it when… |
|---|---|
| Admin UI (admin actions are API-only) | Non-technical admins need self-service |
| Completion screen in the UI (API only) | Site staff record actual costs themselves |
| Retrospective approval for overruns | Finance needs to approve overruns, not just see them |
| Audit entries for admin actions; an audit read endpoint | An auditor asks for them, or threshold changes need to be traceable |
| `Cancelled` state, editing requests, deactivating users or sites | Real users ask for these workflows |
| Multi-role users, multi-level approvals, multi-currency | A client with that structure is onboarded |
| Pagination and sorting | List sizes become a real performance problem |
| Refresh tokens, HttpOnly cookies, RS256, login rate limiting | Moving to production or an external security review |
| Row-Level Security, audit hash chain | A threat model includes direct database access |
| CI pipeline, structured logging and tracing | The code is deployed beyond evaluation |
| Repository layer, separate read store, event sourcing | Aggregates or read load outgrow EF Core with one database |

**Pending:** D-1, a demo-data seed for reviewers, awaits a stakeholder decision ([plan.md](./plan.md#open-decisions)).

---

## 6. Ambiguities resolved by assumption

These cover who completes a request, what counts as "spend" and which date it's recorded on, report access, a single currency, UTC timestamps, Approvers raising requests, and a lone Approver's own requests staying pending. The full list with reasoning is in [PRD §11](./PRD.md#11-assumptions-ambiguities-resolved) (A-1 to A-9).
