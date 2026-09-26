# API Contract — Maintenance Request & Approval System

**Status:** Draft — awaiting approval by lanes A, C, D
**Plan task:** T0.6 · **Related:** [PRD](./PRD.md) · [Architecture §5](./architecture.md#5-api-surface)

This document fixes the request and response shapes for every route in architecture §5. Once lanes A, C and D approve it, it is **frozen**: any change needs agreement from all lanes. It adds no routes beyond architecture §5. Anything that seems missing is listed under [Open questions](#6-open-questions), not added.

---

## 1. Conventions

| Topic | Rule |
|---|---|
| Base path | All routes start with `/api`. The frontend calls relative `/api/...` paths only; nginx proxies them to the API. |
| Format | JSON, UTF-8. Request bodies are sent with `Content-Type: application/json`. Property names are **camelCase**. |
| Authentication | `Authorization: Bearer <jwt>` on every route except `POST /api/auth/login`. |
| IDs | GUID strings, for example `"3f2b8c1e-7a4d-4e5f-9b6a-1c2d3e4f5a6b"`. Route parameters use the `{id:guid}` constraint, so a non-GUID `{id}` returns **404**. |
| Organisation | **Never** sent by the client and **never** returned. No route, query string, request body or response body contains an organisation ID. The organisation comes only from the `org` claim in the token. |
| Money | JSON numbers, at most 2 decimal places, single currency (for example `1250.5` or `1250.50`). Clients must not rely on trailing zeros. |
| Timestamps | ISO-8601 in UTC with a `Z` suffix, for example `"2026-09-26T10:15:30.123Z"`. Fractional seconds may be present. |
| Report dates | Calendar dates as `YYYY-MM-DD` (for example `2026-09-01`), interpreted in UTC. |
| Enums | Sent and returned as **strings**. `status`: `Raised`, `PendingApproval`, `Approved`, `Rejected`, `Completed`. `role`: `SystemAdmin`, `TenantAdmin`, `Requester`, `Approver`. Enum values in requests are matched case-insensitively; responses always use the spelling above. |
| Nulls | Optional fields that have no value are returned as `null`, not omitted. |
| Emails | Trimmed and compared case-insensitively. Must be a valid email address, at most 256 characters, and unique across the whole system. |
| Lists | Returned as a plain JSON array. **No pagination** (future scope, PRD §10). |
| Errors | ProblemDetails (RFC 9457), `Content-Type: application/problem+json`. See [§4](#4-errors). |

### 1.1 Authorization policies

Policies are those in architecture §4.4. The fallback policy requires an authenticated user, so nothing is anonymous except login.

| Policy | Roles allowed |
|---|---|
| `SystemAdmin` | SystemAdmin |
| `TenantAdmin` | TenantAdmin |
| `Requester` | Requester, Approver (an Approver can do everything a Requester can, PRD §4) |
| `Approver` | Approver |
| `ApproverOrTenantAdmin` | Approver, TenantAdmin |
| `OrgMember` | TenantAdmin, Requester, Approver (any user with an organisation) |

A caller with a valid token but a role the policy doesn't allow gets **403**. The policy check runs before the handler, so a 403 from a policy never reveals whether an ID exists.

---

## 2. Authentication

### 2.1 JWT claims

Tokens are HS256-signed JWTs with a 60-minute lifetime (architecture §9). There are no refresh tokens; when a token expires the user logs in again.

| Claim | Value |
|---|---|
| `sub` | User ID (GUID) |
| `org` | Organisation ID (GUID). **Absent** for the System Admin. Server-side use only; the UI must not read or send it. |
| `role` | `SystemAdmin`, `TenantAdmin`, `Requester` or `Approver` |
| `email` | User's email |
| `exp`, `iss`, `aud` | Standard expiry, issuer and audience |

The UI gets the user's ID, email and role from the login response, so it doesn't need to decode the token.

### 2.2 `POST /api/auth/login`

**Policy:** anonymous. **PRD:** FR-1.1, FR-1.3.

**Request**

```json
{
  "email": "alice@acme.example",
  "password": "correct-horse-battery"
}
```

| Field | Rules |
|---|---|
| `email` | Required. |
| `password` | Required. |

Only presence is validated here; password strength rules apply when accounts are created.

**Response `200 OK`**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-09-26T11:15:30Z",
  "user": {
    "id": "8c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f",
    "email": "alice@acme.example",
    "role": "Approver"
  }
}
```

**Errors**

| Status | When |
|---|---|
| 400 | `email` or `password` is missing or empty. |
| 401 | The email doesn't exist **or** the password is wrong. The body is **identical** in both cases (see [§4.3](#43-login-failure-401)). |

---

## 3. Routes

### 3.1 `POST /api/admin/organisations`

Creates an organisation together with its first Tenant Admin, in one save. **Policy:** `SystemAdmin`. **PRD:** FR-2.2, A-1.

**Request**

```json
{
  "name": "Acme Facilities",
  "approvalThreshold": 5000.00,
  "adminEmail": "admin@acme.example",
  "adminPassword": "a-long-password"
}
```

| Field | Rules |
|---|---|
| `name` | Required, 1–200 characters after trimming. |
| `approvalThreshold` | Required, ≥ 0, at most 2 decimal places, at most 1,000,000.00 (the cost cap; see [Open questions](#6-open-questions)). |
| `adminEmail` | Required, valid email, ≤ 256 characters, unique across the system. |
| `adminPassword` | Required, 8–128 characters. |

**Response `201 Created`** (no `Location` header: there is no route to read an organisation)

```json
{
  "name": "Acme Facilities",
  "approvalThreshold": 5000.00,
  "tenantAdmin": {
    "id": "1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d",
    "email": "admin@acme.example",
    "role": "TenantAdmin"
  }
}
```

The new organisation's ID is deliberately not returned (no organisation IDs in DTOs; see [Open questions](#6-open-questions)).

**Errors:** 400 (validation, including `adminEmail` already in use), 401, 403 (caller is not the System Admin).

### 3.2 `POST /api/org/users`

Creates a Requester or Approver in the caller's organisation. **Policy:** `TenantAdmin`. **PRD:** FR-2.3.

**Request**

```json
{
  "email": "bob@acme.example",
  "password": "another-long-password",
  "role": "Requester"
}
```

| Field | Rules |
|---|---|
| `email` | Required, valid email, ≤ 256 characters, unique across the system. |
| `password` | Required, 8–128 characters. |
| `role` | Required. `Requester` or `Approver` only. `TenantAdmin` and `SystemAdmin` are rejected with 400. |

**Response `201 Created`** (no `Location` header: there is no route to read a user)

```json
{
  "id": "5d6e7f8a-9b0c-4d1e-8f2a-3b4c5d6e7f8a",
  "email": "bob@acme.example",
  "role": "Requester"
}
```

**Errors:** 400 (validation, including email already in use), 401, 403 (caller is not a Tenant Admin).

### 3.3 `POST /api/org/sites`

Creates a site in the caller's organisation. **Policy:** `TenantAdmin`. **PRD:** FR-2.4.

**Request**

```json
{
  "name": "Head Office"
}
```

| Field | Rules |
|---|---|
| `name` | Required, 1–200 characters after trimming, unique within the organisation (case-insensitive). |

**Response `201 Created`** (no `Location` header: there is no route to read a single site)

```json
{
  "id": "b7c8d9e0-f1a2-4b3c-8d4e-5f6a7b8c9d0e",
  "name": "Head Office"
}
```

**Errors:** 400 (validation, including a duplicate name in the organisation), 401, 403 (caller is not a Tenant Admin).

### 3.4 `PUT /api/org/threshold`

Sets the caller's organisation's approval threshold. **Policy:** `TenantAdmin`. **PRD:** FR-2.5, FR-4.3.

**Request**

```json
{
  "approvalThreshold": 7500.00
}
```

| Field | Rules |
|---|---|
| `approvalThreshold` | Required, ≥ 0, at most 2 decimal places, at most 1,000,000.00 (see [Open questions](#6-open-questions)). |

**Response `200 OK`**

```json
{
  "approvalThreshold": 7500.00
}
```

The change applies to requests created from now on. Requests already `Approved` or `Rejected` keep their `thresholdAtDecision`; requests already in `PendingApproval` stay pending (FR-4.3).

**Errors:** 400 (validation), 401, 403 (caller is not a Tenant Admin).

### 3.5 `GET /api/sites`

Lists the caller's organisation's sites, for the site dropdown on the create-request screen. **Policy:** `OrgMember`. **PRD:** FR-2.6.

**Response `200 OK`**, ordered by `name` ascending:

```json
[
  { "id": "b7c8d9e0-f1a2-4b3c-8d4e-5f6a7b8c9d0e", "name": "Head Office" },
  { "id": "c8d9e0f1-a2b3-4c4d-9e5f-6a7b8c9d0e1f", "name": "Warehouse North" }
]
```

An organisation with no sites returns `[]`.

**Errors:** 401, 403 (System Admin, who has no organisation).

### 3.6 `POST /api/requests`

Raises a maintenance request. The server routes it immediately: below the threshold it becomes `Approved`, at or above the threshold it becomes `PendingApproval` (FR-4.1, A-2). Creation and routing are saved together and both are audited (FR-3.3). **Policy:** `Requester` (Requester or Approver). **PRD:** FR-3, FR-4.1, FR-4.2.

**Request**

```json
{
  "siteId": "b7c8d9e0-f1a2-4b3c-8d4e-5f6a7b8c9d0e",
  "description": "Replace broken air-conditioning unit on floor 3.",
  "estimatedCost": 6200.00
}
```

| Field | Rules |
|---|---|
| `siteId` | Required, GUID. Must be a site in the caller's organisation; otherwise **404**. |
| `description` | Required, not blank, at most 2,000 characters. |
| `estimatedCost` | Required, > 0, at most 2 decimal places, at most 1,000,000.00. |

**Response `201 Created`**, with `Location: /api/requests/{id}` and a [`RequestDetail`](#313-request-shapes) body. The `status` in the body is already `Approved` or `PendingApproval`, never `Raised`.

**Errors**

| Status | When |
|---|---|
| 400 | Validation failed. |
| 401 | Missing, invalid or expired token. |
| 403 | Caller is a Tenant Admin or the System Admin. |
| 404 | `siteId` doesn't exist or belongs to another organisation. |

### 3.7 `GET /api/requests?status=`

Lists requests. **Policy:** `Requester` (Requester or Approver). **PRD:** FR-5.1, FR-5.2.

**Visibility**

| Caller | Sees |
|---|---|
| Requester | Only the requests they raised. |
| Approver | All requests in their organisation, including their own. |

**Query parameters**

| Parameter | Rules |
|---|---|
| `status` | Optional. One of the five status values. When present, only requests in that status are returned (applied on top of the visibility rule above). When absent, all visible requests are returned. An unknown value returns 400. |

**Ordering:** `createdAt` descending (newest first). No pagination.

**Response `200 OK`**: an array of [`RequestSummary`](#313-request-shapes):

```json
[
  {
    "id": "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b",
    "siteId": "b7c8d9e0-f1a2-4b3c-8d4e-5f6a7b8c9d0e",
    "siteName": "Head Office",
    "description": "Replace broken air-conditioning unit on floor 3.",
    "estimatedCost": 6200.00,
    "status": "PendingApproval",
    "raisedByUserId": "5d6e7f8a-9b0c-4d1e-8f2a-3b4c5d6e7f8a",
    "createdAt": "2026-09-26T10:15:30Z"
  },
  {
    "id": "f2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c",
    "siteId": "c8d9e0f1-a2b3-4c4d-9e5f-6a7b8c9d0e1f",
    "siteName": "Warehouse North",
    "description": "Fix leaking tap in staff kitchen.",
    "estimatedCost": 120.00,
    "status": "Approved",
    "raisedByUserId": "5d6e7f8a-9b0c-4d1e-8f2a-3b4c5d6e7f8a",
    "createdAt": "2026-09-25T08:02:11Z"
  }
]
```

No visible requests returns `[]`.

**Errors:** 400 (invalid `status`), 401, 403 (Tenant Admin or System Admin).

### 3.8 `GET /api/requests/{id}`

Returns one request. **Policy:** `Requester` (Requester or Approver). **PRD:** FR-5.1, FR-5.2, FR-5.3.

Visibility is the same as the list: a Requester can read only requests they raised; an Approver can read any request in their organisation.

**Response `200 OK`**: a [`RequestDetail`](#313-request-shapes).

**Errors**

| Status | When |
|---|---|
| 401 | Missing, invalid or expired token. |
| 403 | Caller is a Tenant Admin or the System Admin. |
| 404 | The request doesn't exist, belongs to another organisation, or (for a Requester) was raised by someone else. The three cases are indistinguishable. |

### 3.9 `POST /api/requests/{id}/approve`

Approves a pending request. The threshold in force now is stored as `thresholdAtDecision` (FR-4.2). **Policy:** `Approver`. **PRD:** FR-3, FR-3.4, FR-3.5.

**Request** (the body is required; send `{}` when there is no comment)

```json
{
  "comment": "Approved, go ahead with the cheaper quote."
}
```

| Field | Rules |
|---|---|
| `comment` | Optional (`null`, omitted or a string). At most 2,000 characters. |

**Response `200 OK`**: the updated [`RequestDetail`](#313-request-shapes), with `status` `Approved`.

**Errors**

| Status | When |
|---|---|
| 400 | `comment` is longer than 2,000 characters. |
| 401 | Missing, invalid or expired token. |
| 403 | Caller is not an Approver, **or** the caller raised this request (self-approval, `SelfApprovalException`). |
| 404 | The request doesn't exist or belongs to another organisation. |
| 409 | The request is not in `PendingApproval` (`InvalidTransitionException`), or another decision on it was saved at the same time (concurrency conflict). |

### 3.10 `POST /api/requests/{id}/reject`

Rejects a pending request. `Rejected` is terminal. **Policy:** `Approver`. **PRD:** FR-3, FR-3.2, FR-3.4, FR-3.5.

**Request**: same shape and rules as approve.

```json
{
  "comment": "Out of budget this quarter."
}
```

**Response `200 OK`**: the updated [`RequestDetail`](#313-request-shapes), with `status` `Rejected`.

**Errors**: same as approve (400, 401, 403 including self-rejection, 404, 409).

### 3.11 `POST /api/requests/{id}/complete`

Completes an approved request and records its actual cost. On an overrun the request still completes and `exceededThreshold` is set to `true` (FR-4.4). `Completed` is terminal. API only; there is no UI screen for it (PRD §8). **Policy:** `Requester` (Requester or Approver), plus the ownership rule below. **PRD:** FR-3, FR-4.4, A-5.

**Who may complete**

| Caller | Allowed |
|---|---|
| Requester | Only requests they raised. Someone else's request returns **404**, because a Requester can't see it. |
| Approver | Any request in their organisation, including their own. |
| Tenant Admin, System Admin | No (403 from the policy). |

**Request**

```json
{
  "actualCost": 6450.00
}
```

| Field | Rules |
|---|---|
| `actualCost` | Required, > 0, at most 2 decimal places, at most 1,000,000.00. |

**Response `200 OK`**: the updated [`RequestDetail`](#313-request-shapes), with `status` `Completed` and `completedAt` set.

**Errors**

| Status | When |
|---|---|
| 400 | Validation failed. |
| 401 | Missing, invalid or expired token. |
| 403 | Caller is a Tenant Admin or the System Admin. |
| 404 | The request doesn't exist, belongs to another organisation, or (for a Requester) was raised by someone else. |
| 409 | The request is not in `Approved`, or a concurrent change was saved first. |

### 3.12 `GET /api/reports/spend?from=&to=`

Spend per site for a date range. **Policy:** `ApproverOrTenantAdmin`. **PRD:** FR-6, A-6, A-7.

**Query parameters**

| Parameter | Rules |
|---|---|
| `from` | Required, `YYYY-MM-DD`. |
| `to` | Required, `YYYY-MM-DD`. Must satisfy `from ≤ to`. |

Both dates are **inclusive** and in UTC: a request counts when `from 00:00:00Z ≤ completedAt < (to + 1 day) 00:00:00Z`. Spend is the sum of `actualCost` of `Completed` requests only (FR-6.2).

**Response `200 OK`**

```json
{
  "from": "2026-09-01",
  "to": "2026-09-30",
  "rows": [
    {
      "siteId": "b7c8d9e0-f1a2-4b3c-8d4e-5f6a7b8c9d0e",
      "siteName": "Head Office",
      "totalSpend": 6450.00
    },
    {
      "siteId": "c8d9e0f1-a2b3-4c4d-9e5f-6a7b8c9d0e1f",
      "siteName": "Warehouse North",
      "totalSpend": 0
    }
  ],
  "grandTotal": 6450.00
}
```

- `rows` has **exactly one row per site** in the caller's organisation, including sites with zero spend (`totalSpend: 0`), ordered by `siteName` ascending (FR-6.3).
- `grandTotal` is the sum of all `totalSpend` values.
- An organisation with no sites returns `"rows": []` and `"grandTotal": 0`.

**Errors**

| Status | When |
|---|---|
| 400 | `from` or `to` is missing or not a valid `YYYY-MM-DD` date, or `from > to`. |
| 401 | Missing, invalid or expired token. |
| 403 | Caller is a Requester or the System Admin. |

### 3.13 Request shapes

Referenced above. Both are built with manual mapping (a `Select` projection or `ToDto()`).

**`RequestSummary`** (list items)

| Field | Type | Notes |
|---|---|---|
| `id` | GUID | |
| `siteId` | GUID | |
| `siteName` | string | |
| `description` | string | |
| `estimatedCost` | number | |
| `status` | string | One of the five statuses. |
| `raisedByUserId` | GUID | |
| `createdAt` | timestamp | |

**`RequestDetail`** (single request, and the result of create, approve, reject and complete)

| Field | Type | Notes |
|---|---|---|
| `id` | GUID | |
| `siteId` | GUID | |
| `siteName` | string | |
| `raisedByUserId` | GUID | The UI compares it with the logged-in user's `id` to hide Approve/Reject on the user's own requests (UX only; the server enforces it). |
| `raisedByEmail` | string | Shown on the Approve/Reject screen. |
| `description` | string | |
| `estimatedCost` | number | |
| `actualCost` | number or `null` | Set on completion. |
| `status` | string | One of the five statuses. |
| `thresholdAtDecision` | number or `null` | Threshold in force when the request was approved (automatically or manually). `null` while pending, and when rejected. |
| `exceededThreshold` | boolean | `true` only for a completed request with an overrun (FR-4.4). |
| `createdAt` | timestamp | |
| `completedAt` | timestamp or `null` | |

Example:

```json
{
  "id": "e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b",
  "siteId": "b7c8d9e0-f1a2-4b3c-8d4e-5f6a7b8c9d0e",
  "siteName": "Head Office",
  "raisedByUserId": "5d6e7f8a-9b0c-4d1e-8f2a-3b4c5d6e7f8a",
  "raisedByEmail": "bob@acme.example",
  "description": "Replace broken air-conditioning unit on floor 3.",
  "estimatedCost": 6200.00,
  "actualCost": null,
  "status": "PendingApproval",
  "thresholdAtDecision": null,
  "exceededThreshold": false,
  "createdAt": "2026-09-26T10:15:30Z",
  "completedAt": null
}
```

The audit history is not returned; an audit read endpoint is future scope (PRD §10).

---

## 4. Errors

All errors produced by the API's exception handler are ProblemDetails (RFC 9457) with `Content-Type: application/problem+json`. Handlers only throw the defined exceptions; the central handler builds the response (architecture §4.4).

| Status | Cause |
|---|---|
| 400 | `ValidationException` (FluentValidation), or a body/query value that can't be parsed (malformed JSON, wrong type, invalid date or enum). |
| 401 | Missing, invalid or expired token; or failed login. |
| 403 | The endpoint's role policy rejects the caller; or a handler throws `ForbiddenException` (application ownership rule) or the domain throws `SelfApprovalException` (self-approval or self-rejection). |
| 404 | `NotFoundException`: the ID doesn't exist, belongs to another organisation, or isn't visible to the caller. **Another tenant's data always gives 404, never 403.** |
| 409 | `InvalidTransitionException` (illegal state change) or `DbUpdateConcurrencyException` (a concurrent change was saved first). |

A 401 or 403 produced by the authentication/authorization middleware (no token, wrong role) may have an **empty body**. Clients must act on the status code, not on the body. Any ProblemDetails may also carry a `traceId` extension member.

### 4.1 Validation error (400)

`errors` maps each failing field (camelCase, matching the request property or query parameter name) to one or more messages. Message wording is not part of the contract; the keys are.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "description": ["Description is required."],
    "estimatedCost": ["Estimated cost must be greater than 0."]
  }
}
```

### 4.2 Not found (404)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "The requested resource was not found."
}
```

The `detail` is the same whether the ID is missing, belongs to another organisation, or isn't visible to the caller.

### 4.3 Login failure (401)

Returned whenever the email is unknown or the password is wrong. The body is byte-for-byte the same in both cases.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid email or password."
}
```

### 4.4 Other examples

403 (self-approval):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You cannot approve or reject a request you raised."
}
```

409 (illegal transition or concurrent change):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "The request is not in a state that allows this action."
}
```

---

## 5. Route summary

| Method & route | Policy | Body | Success |
|---|---|---|---|
| `POST /api/auth/login` | Anonymous | `{ email, password }` | 200 `{ accessToken, expiresAt, user }` |
| `POST /api/admin/organisations` | SystemAdmin | `{ name, approvalThreshold, adminEmail, adminPassword }` | 201 `{ name, approvalThreshold, tenantAdmin }` |
| `POST /api/org/users` | TenantAdmin | `{ email, password, role }` | 201 `{ id, email, role }` |
| `POST /api/org/sites` | TenantAdmin | `{ name }` | 201 `{ id, name }` |
| `PUT /api/org/threshold` | TenantAdmin | `{ approvalThreshold }` | 200 `{ approvalThreshold }` |
| `GET /api/sites` | OrgMember | — | 200 `[{ id, name }]` |
| `POST /api/requests` | Requester (Requester, Approver) | `{ siteId, description, estimatedCost }` | 201 `RequestDetail` |
| `GET /api/requests?status=` | Requester (Requester, Approver) | — | 200 `RequestSummary[]` |
| `GET /api/requests/{id}` | Requester (Requester, Approver) | — | 200 `RequestDetail` |
| `POST /api/requests/{id}/approve` | Approver | `{ comment? }` | 200 `RequestDetail` |
| `POST /api/requests/{id}/reject` | Approver | `{ comment? }` | 200 `RequestDetail` |
| `POST /api/requests/{id}/complete` | Requester (Requester, Approver) + raiser-or-Approver rule | `{ actualCost }` | 200 `RequestDetail` |
| `GET /api/reports/spend?from=&to=` | ApproverOrTenantAdmin | — | 200 `{ from, to, rows, grandTotal }` |

---

## 6. Open questions

For the approving lanes to settle. Nothing here has been added to the contract beyond what is stated above.

1. **Policy name for "Requester, Approver" (lane C).** Architecture §4.4 has no policy named for "Requester or Approver". This contract assumes the `Requester` policy admits both roles (PRD §4: an Approver can do everything a Requester can). Confirm, or name a separate policy.
2. **Global email uniqueness vs. query filters (lanes B, C).** Emails are unique system-wide (PRD §7), but `POST /api/org/users` is a Tenant Admin handler, which may not call `IgnoreQueryFilters()`. A pre-check through the filtered `Users` set can't see other organisations' users. How should the handler detect a duplicate so it returns 400 rather than 500 (for example, by relying on the unique index)? Note also that a 400 "email already in use" reveals that the email exists in some organisation; this seems unavoidable given the uniqueness rule.
3. **New organisation's ID in the create-organisation response.** Omitted because `CLAUDE.md` forbids organisation IDs in DTOs, and no route would use it. Confirm that's acceptable for the System Admin.
4. **Upper bound for the threshold.** The PRD only says ≥ 0. This contract caps it at 1,000,000.00 to match the cost cap (a threshold above the maximum cost would mean every request is auto-approved anyway). Confirm, or state another bound.
5. **Limits the PRD doesn't define.** Organisation and site names 1–200 characters; approve/reject comments ≤ 2,000 characters; passwords 8–128 characters; emails ≤ 256 characters. Confirm; once approved they should get a line in `docs/DECISIONS.md`.
6. **No way to read the current threshold.** Architecture §5 has no `GET` for it, so a Tenant Admin can only learn it from the response to `PUT /api/org/threshold`. Not needed by any UI screen; flagged in case a reviewer asks.
7. **UTC timestamps on the wire (lanes B, C).** `datetime2` values read by EF Core come back with `DateTimeKind.Unspecified` and would serialise without the `Z`. The implementation needs to guarantee the `Z` (for example with a UTC value converter).
