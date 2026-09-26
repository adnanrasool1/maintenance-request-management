# PRD — Maintenance Request & Approval System (Backend)

**Status:** Draft v1
**Source:** Technical Evaluation Task Brief (Senior Backend Engineer)
**Effort budget:** 4–6 hours. Anything outside this document goes to [Future Scope](#10-future-scope).

---

## 1. Problem

A facilities management company tracks maintenance requests across client sites using email and spreadsheets. Requests get lost, approvals can't be traced, and spend per site can't be reported.

## 2. Goal

A multi-tenant backend (with a minimal UI) that:

- records maintenance requests against sites,
- routes them through cost-based approval,
- keeps a tamper-resistant audit trail, and
- reports spend per site for a date range,

with strict data isolation between client organisations.

## 3. Success criteria

| # | Criterion |
|---|---|
| SC-1 | A user in Org A can never read or change Org B data, including by changing IDs in requests. |
| SC-2 | Illegal state transitions and unauthorised actions are rejected by the server. |
| SC-3 | The spend report returns correct totals for the caller's organisation only. |
| SC-4 | Every state change and approval decision has an audit record (who, what, when) that the application cannot edit or delete. |
| SC-5 | The system starts from the README in under 15 minutes on a clean machine. |

---

## 4. Roles

| Role | Scope | Can do |
|---|---|---|
| **System Admin** | Platform (no organisation) | Create organisations and their Tenant Admins. Seeded at startup. Cannot read tenant request data. |
| **Tenant Admin** | One organisation | Create users (Requester / Approver) and sites in their org. Set the org's approval threshold. View the spend report. |
| **Requester** | One organisation | Raise requests. View their own requests. Complete their own approved requests. |
| **Approver** | One organisation | Everything a Requester can do, plus: view all org requests, approve or reject pending requests (except their own), view the spend report. |

Roles are enforced on the server for every endpoint. Each user has exactly one role.

---

## 5. Domain model

| Entity | Key fields |
|---|---|
| **Organisation** | Id, Name, ApprovalThreshold (decimal, ≥ 0) |
| **Site** | Id, OrganisationId, Name |
| **User** | Id, OrganisationId (null for System Admin), Email (unique), PasswordHash, Role |
| **MaintenanceRequest** | Id, OrganisationId, SiteId, RaisedByUserId, Description, EstimatedCost, ActualCost (nullable), Status, ThresholdAtDecision, ExceededThreshold (bool), CreatedAt, CompletedAt |
| **AuditEntry** | Id, OrganisationId, RequestId, ActorUserId (null = system), Action, FromStatus, ToStatus, Comment, OccurredAt (UTC) |

The Site for a request must belong to the caller's organisation.

---

## 6. Functional requirements

### FR-1 Authentication

- FR-1.1 Users log in with email and password and receive a token.
- FR-1.2 Passwords are stored hashed, never in plain text.
- FR-1.3 The caller's organisation and role come **only** from the authenticated identity, never from the request body, query string or route.

### FR-2 Tenant administration (confirmed with stakeholder)

- FR-2.1 The System Admin account is seeded at startup. Its credentials come from configuration and are not in source control.
- FR-2.2 The System Admin creates an organisation together with its first Tenant Admin.
- FR-2.3 A Tenant Admin creates Requester and Approver users in their own organisation.
- FR-2.4 A Tenant Admin creates sites in their own organisation.
- FR-2.5 A Tenant Admin sets the organisation's approval threshold. The value is stored per organisation in the database. No other role can change it.
- FR-2.6 Any authenticated org user can list their organisation's sites. This populates the site picker on the create-request screen.

### FR-3 Request lifecycle

States: `Raised`, `PendingApproval`, `Approved`, `Rejected`, `Completed`.

| From | To | Who | Condition |
|---|---|---|---|
| — | `Raised` | Requester / Approver | Request created |
| `Raised` | `Approved` | System (automatic) | EstimatedCost **<** threshold |
| `Raised` | `PendingApproval` | System (automatic) | EstimatedCost **≥** threshold |
| `PendingApproval` | `Approved` | Approver | Approver is not the request's raiser |
| `PendingApproval` | `Rejected` | Approver | Approver is not the request's raiser |
| `Approved` | `Completed` | Raiser, or an Approver in the org | ActualCost provided |

- FR-3.1 Every transition not in the table is rejected.
- FR-3.2 `Rejected` and `Completed` are terminal.
- FR-3.3 Creating a request and its automatic routing happen in one transaction. Both steps are audited.
- FR-3.4 Approve and reject may include an optional comment.
- FR-3.5 Concurrent decisions on the same request (for example, two Approvers at once) must not both succeed.

### FR-4 Threshold behaviour

- FR-4.1 Requests with EstimatedCost below the threshold are approved automatically. Requests at or above it need an Approver.
- FR-4.2 When a request is approved, automatically or manually, the threshold in force at that moment is stored on the request (`ThresholdAtDecision`).
- FR-4.3 Changing the threshold does not affect requests that were already approved or rejected. Requests already in `PendingApproval` stay pending.
- FR-4.4 **Overrun decision:** A request overruns when its ActualCost is more than was authorised:
  - **auto-approved:** ActualCost **≥** `ThresholdAtDecision` (it would have needed an Approver, consistent with A-2);
  - **manually approved:** ActualCost **>** EstimatedCost (more was spent than the Approver approved).

  On an overrun the request still becomes `Completed`, `ExceededThreshold` is set to true, and the audit entry records the overrun. Blocking completion would not undo money that has already been spent, so the goal is to make the overrun visible and traceable. A retrospective approval workflow is listed in Future Scope.

### FR-5 Request queries

- FR-5.1 Requesters see only the requests they raised.
- FR-5.2 Approvers see all requests in their organisation. They can filter by status to find pending items.
- FR-5.3 Fetching a request that doesn't exist or belongs to another organisation returns **404**, so the response doesn't reveal whether the ID exists.

### FR-6 Spend report

- FR-6.1 `GET` spend report with `from` and `to` dates, available to Approvers and Tenant Admins.
- FR-6.2 Spend is the sum of `ActualCost` for `Completed` requests whose `CompletedAt` falls within the range. `from` and `to` are both inclusive calendar dates in UTC.
- FR-6.3 The report returns one row per site in the caller's organisation, including sites with zero spend.
- FR-6.4 The organisation filter comes from the caller's identity only.

### FR-7 Audit trail

- FR-7.1 An audit entry is written for every state change and every approval decision. It records the actor (or system), the action, the from and to states, an optional comment, and a UTC timestamp.
- FR-7.2 The audit entry is written in the same transaction as the change it records. If one fails, both fail.
- FR-7.3 The audit trail is append-only. The application exposes no update or delete for it, and the database role the application uses has only `INSERT`/`SELECT` permission on the audit table (or an equivalent database guard).

---

## 7. Non-functional requirements

| Area | Requirement |
|---|---|
| **Tenant isolation** | Enforced centrally in the data-access layer, not repeated by hand in each endpoint, and backed by explicit checks on writes. Every tenant-owned table has an `OrganisationId`. |
| **Authorisation** | All role and ownership checks run on the server. Hiding UI elements is only for convenience. |
| **Input validation** | Checked at the API boundary. Description: required, max 2,000 characters. Costs (estimated and actual): > 0, at most 2 decimal places, at most 1,000,000.00. Report: `from ≤ to`. Emails must be valid and unique. |
| **Secrets** | Nothing sensitive in the repository. Locally: user-secrets or `.env` (git-ignored), with a committed `.env.example`. Production: a managed secret store. This is documented, not built. |
| **Money** | Stored as decimal, never floating point. Single currency. |
| **Data design** | Schema created through migrations. Indexes support the main queries: requests by org and status, requests by org, site and completion date, and audit entries by request. |

---

## 8. Frontend (minimal)

Four screens: **Login**, **Request list**, **Create request** (with site dropdown), and **Approve / Reject** (for Approvers on pending requests).
Plain styling is fine, but every screen must work. Admin and completion actions are available through the API only (see Future Scope).

---

## 9. Deliverables

1. `README.md`: run instructions that work in under 15 minutes on a clean machine.
2. `DECISIONS.md`: one page covering architecture, data access, auth, structure, libraries, what was rejected and why, and the assumptions in §11.
3. `AI-LOG.md`: one page covering what was delegated fully, with constraints, or done by hand; at least one real prompt; and one plausible-but-wrong agent output, including how it was caught.
4. Agent configuration (`CLAUDE.md` or equivalent), committed.
5. Real commit history, not squashed.
6. Focused tests covering: cross-tenant access blocked (including ID manipulation), illegal transitions rejected, self-approval blocked, threshold routing at the boundary value, and spend report totals.
7. A short (~2 minute) walkthrough video before the review. Preferred, not required.

---

## 10. Future scope

Not built. These are recorded here instead of being added now.

- Admin UI for System Admin and Tenant Admin actions (API only for now).
- Completion screen in the UI (API only for now).
- A retrospective approval workflow when an actual cost overruns the threshold.
- Audit entries for admin actions (user or site creation, threshold changes).
- An endpoint to read a request's audit history.
- A `Cancelled` state and request editing before a decision.
- Deactivating users and sites.
- Users with more than one role, and multi-level approval chains.
- Multi-currency support.
- Pagination and sorting on list endpoints.
- Token refresh and revocation, and account lockout.
- Production secret store integration.

**Out of scope according to the brief:** visual design, deployment, file uploads, notifications, password reset, background jobs, exhaustive testing, and performance tuning beyond sensible schema and query design.

---

## 11. Assumptions (ambiguities resolved)

| # | Assumption |
|---|---|
| A-1 | The System Admin creates the organisation when creating its first Tenant Admin, because a Tenant Admin needs an organisation to belong to. |
| A-2 | "Below the threshold skips approval" means a cost **equal** to the threshold requires approval. |
| A-3 | Approvers can raise requests, as the rule "cannot approve their own request" implies. Another Approver must approve them. |
| A-4 | If an organisation has only one Approver, that Approver's own over-threshold requests stay pending until another Approver exists. This is intended. |
| A-5 | Completion is performed by the raiser or an Approver. Tenant Admins do not take part in the request workflow. |
| A-6 | "Spend" means the actual cost of completed requests, dated by completion date. Estimates and incomplete work are excluded. |
| A-7 | Requesters cannot view the spend report, because it contains organisation-wide financial data. |
| A-8 | The System Admin has no access to tenant request data or reports. |
| A-9 | All timestamps are stored in UTC, and there is a single currency. |
