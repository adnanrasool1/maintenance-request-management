# Maintenance Request & Approval System

A multi-tenant backend, with a minimal Angular UI, that replaces email and spreadsheets for facilities maintenance. Site staff raise requests, requests above an organisation's cost threshold need approval, every decision is audited, and spend can be reported per site.

> **Status:** feature-complete for the PRD. Progress and what is left is in [docs/plan.md](docs/plan.md).

---

## Documentation

| Document | What it covers |
|---|---|
| [docs/task-brief.md](docs/task-brief.md) | The original evaluation brief |
| [docs/PRD.md](docs/PRD.md) | Requirements, roles, lifecycle, assumptions and future scope |
| [docs/architecture.md](docs/architecture.md) | Stack, Clean Architecture/CQRS, data model, tenant isolation, audit integrity |
| [docs/api-contract.md](docs/api-contract.md) | Endpoints, request and response shapes, errors |
| [docs/DECISIONS.md](docs/DECISIONS.md) | Key technical choices, what was rejected, and assumptions |
| [docs/AI-LOG.md](docs/AI-LOG.md) | How AI agents were used, including where they got it wrong |
| [docs/plan.md](docs/plan.md) | Delivery plan and progress |
| [docs/git-rules.md](docs/git-rules.md) | Branching, commits, PRs, and rules for AI agents |
| [CLAUDE.md](CLAUDE.md) | Agent configuration (with [`.claude/settings.json`](.claude/settings.json)) |

---

## Prerequisites

- **Docker Desktop** (or Docker Engine with Compose v2). Nothing else is required.
- About 4 GB of free RAM for the SQL Server container.

> **Apple Silicon (M1–M4):** the SQL Server image is built for amd64 only. In Docker Desktop, go to **Settings → General** and turn on **"Use Rosetta for x86_64/amd64 emulation on Apple Silicon"** before starting.

---

## Run it

```bash
git clone <repo-url>
cd <repo>/infra

./setup.sh                  # Windows: .\setup.ps1
docker compose up --build
```

`setup.sh` creates `infra/.env` with random passwords and a random JWT key. It only does this if the file doesn't already exist, and the file is never committed. On Windows, if script execution is blocked, run `powershell -ExecutionPolicy Bypass -File .\setup.ps1`.

With the images already downloaded, `docker compose up --build` takes well under a minute. On a clean machine most of the time is the first download of the SQL Server image (about 500 MB) and the .NET and Node build images.

On first start, services come up in this order: SQL Server → migrator (applies the schema, creates the restricted database login, seeds the System Admin) → API → web. Wait until the `api` and `web` containers are running.

| What | URL |
|---|---|
| Web UI | http://localhost:8080 |
| API reference (Scalar) | http://localhost:5080/scalar |

The local stack runs the API in the `Development` environment so that Scalar is available. A production deployment would not enable it.

To stop the stack, run `docker compose down`. To also delete the database, run `docker compose down -v`.

---

## First login and setup

The system starts with only a **System Admin**. Its email and password are the `SYSTEM_ADMIN_EMAIL` and `SYSTEM_ADMIN_PASSWORD` values in `infra/.env`.

The admin steps are available through the API only, using Scalar at http://localhost:5080/scalar. Call `POST /api/auth/login` first, copy `accessToken` from the response, and paste it into Scalar's **Authentication → Bearer** field so the following calls send it. Log in again whenever you switch user.

1. **As the System Admin:** `POST /api/admin/organisations` creates an organisation and its Tenant Admin.
   ```json
   { "name": "Acme Facilities", "approvalThreshold": 5000, "adminEmail": "admin@acme.example", "adminPassword": "a-long-password" }
   ```
2. **As the Tenant Admin** (log in with the `adminEmail` above):
   - `POST /api/org/sites` → `{ "name": "Head Office" }`
   - `POST /api/org/users` → `{ "email": "bob@acme.example", "password": "a-long-password", "role": "Requester" }`, then two users with `"role": "Approver"` (an Approver can't approve their own request)
   - `PUT /api/org/threshold` → `{ "approvalThreshold": 5000 }` to change the threshold later
3. **In the web UI:** log in as the Requester and raise a request. Below the threshold it is approved automatically; at or above it, log in as an Approver to approve or reject it.
4. **Completing a request** is API-only (PRD §8): `POST /api/requests/{id}/complete` with `{ "actualCost": 5200 }`, as the raiser or an Approver. Then `GET /api/reports/spend?from=2026-09-01&to=2026-09-30` as an Approver or the Tenant Admin shows spend per site.

The full request and response shapes are in [docs/api-contract.md](docs/api-contract.md).

To see tenant isolation in action, create a second organisation and try to open the first organisation's request IDs.

---

## Run the tests

The tests need the .NET 10 SDK and a running Docker, because the integration tests start their own SQL Server with Testcontainers.

```bash
dotnet test backend/MaintenanceApprovals.slnx
```

The tests are deliberately focused on the risks the brief names, not on coverage:

| Risk (brief / PRD §9) | Where it is tested |
|---|---|
| Cross-tenant access, including ID manipulation | `Mra.Api.IntegrationTests`: request handlers with another organisation's IDs return 404 and leave its data unchanged; query filters; the composite FK rejects another tenant's site |
| Illegal state transitions | `Mra.Domain.Tests`: every illegal state/action pair |
| Self-approval | `Mra.Domain.Tests` and the approve/reject handler tests |
| Threshold routing at the boundary | `Mra.Domain.Tests` (equal to the threshold needs approval) and the create-request handler |
| Spend report totals | `Mra.Api.IntegrationTests`: both range edges, a zero-spend site, the other tenant excluded |
| Role policies | Policy × role matrix |
| Audit integrity | Connected as `mra_app`, `UPDATE` and `DELETE` on `AuditEntries` fail |

The reasons for choosing these tests are in [docs/architecture.md §13](docs/architecture.md#13-testing-strategy).

---

## Repository layout

```
backend/    .NET 10 solution (Clean Architecture + CQRS)
frontend/   Angular 22 app
infra/      docker-compose, .env template, setup scripts
docs/       all project documentation
```

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `sqlserver` keeps restarting | Give Docker at least 4 GB of memory. On Apple Silicon, check that Rosetta is on (see above). |
| `migrator` exits with an error | Run `docker compose logs migrator`. Usually SQL Server wasn't ready yet; run `docker compose up` again. |
| Port 8080, 5080 or 1433 already in use | Change `WEB_HOST_PORT`, `API_HOST_PORT` or `SQL_HOST_PORT` in `infra/.env` (for example `SQL_HOST_PORT=14330` when a local SQL Server already uses 1433), or stop whatever is using the port. |
| Connecting with SSMS or another SQL client | Server `localhost,<SQL_HOST_PORT>` (a comma, not a colon), **SQL Server Authentication**, login `sa` with `MSSQL_SA_PASSWORD` from `infra/.env`, and tick **Trust server certificate**. The API itself connects as the restricted `mra_app` login. |
| Login fails for the System Admin | Check the credentials in `infra/.env`. If you changed them after the first run, reset with `docker compose down -v`. |
