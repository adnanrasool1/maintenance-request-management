# Maintenance Request & Approval System

A multi-tenant backend, with a minimal Angular UI, that replaces email and spreadsheets for facilities maintenance. Site staff raise requests, requests above an organisation's cost threshold need approval, every decision is audited, and spend can be reported per site.

> **Status:** in development. The steps below describe the target setup (plan task T12.1 finalises them). Checklist of progress: [docs/plan.md](docs/plan.md).

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

`setup.sh` creates `infra/.env` with random passwords and a random JWT key. It only does this if the file doesn't already exist, and the file is never committed.

On first start, services come up in this order: SQL Server → migrator (applies the schema, creates the restricted database login, seeds the System Admin) → API → web. Wait until the `api` and `web` containers are running.

| What | URL |
|---|---|
| Web UI | http://localhost:8080 |
| API reference (Scalar) | http://localhost:5080/scalar |

To stop the stack, run `docker compose down`. To also delete the database, run `docker compose down -v`.

---

## First login and setup

The system starts with only a **System Admin**. Its email and password are the `SYSTEM_ADMIN_EMAIL` and `SYSTEM_ADMIN_PASSWORD` values in `infra/.env`.

The admin steps are available through the API only, using Scalar at http://localhost:5080/scalar. Call `POST /api/auth/login` first and use the returned token for the rest of the calls.

1. **As the System Admin:** `POST /api/admin/organisations` creates an organisation and its Tenant Admin.
2. **As the Tenant Admin:**
   - `POST /api/org/sites` to create sites
   - `POST /api/org/users` to create a Requester and at least **two** Approvers (an Approver can't approve their own request)
   - `PUT /api/org/threshold` to set the approval threshold
3. **In the web UI:** log in as a Requester, raise a request, then log in as an Approver to approve or reject it.

To see tenant isolation in action, create a second organisation and try to open the first organisation's request IDs.

---

## Run the tests

The tests need the .NET 10 SDK and a running Docker, because the integration tests start their own SQL Server with Testcontainers.

```bash
dotnet test backend/MaintenanceApprovals.slnx
```

| Suite | What it covers |
|---|---|
| `Mra.Domain.Tests` | State transitions, threshold boundary, self-approval, overrun flag |
| `Mra.Api.IntegrationTests` | Cross-tenant access, role policies, spend report totals, audit table immutability |

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
| Port 8080, 5080 or 1433 already in use | Change the host port in `infra/.env`, or stop whatever is using the port. |
| Login fails for the System Admin | Check the credentials in `infra/.env`. If you changed them after the first run, reset with `docker compose down -v`. |
