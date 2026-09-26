# Technical Evaluation — Task Brief

**Role:** Senior Backend Engineer
**Expected effort:** 4–6 hours
**Review:** 30-minute session where we discuss what you built.

---

## How to read this brief

We use AI-led development as our primary delivery mode. Engineers are expected to spend their time specifying work, steering agents through implementation, and verifying what comes back.

**You are expected to use AI agents for this exercise.** Claude Code, Cursor, Copilot — whatever your normal setup is. This is not a test of whether you can write C# from memory.

We care more about how you worked than how much you built. A small, well-reasoned system you can defend beats a larger one you cannot explain. We have deliberately kept the scope modest — please respect it rather than expanding it. Focus on the key decisions on architecture, design patterns and choice of libraries and packages utilized.

This brief is incomplete in places. Where something is unspecified, make a decision, document it, and be ready to justify it.

---

## The problem

A facilities management company services equipment across sites operated by different client organisations. Site staff raise maintenance requests; those requests cost money; money needs approval before work proceeds.

They currently run this on email and spreadsheets. Requests get lost, approvals are untraceable, and nobody can answer *"what did we spend at Site 12 last month?"*

Build the backend for a system that fixes this.

---

## Core domain

- **Organisations** — client companies. Data must be strictly isolated between them.
- **Sites** — locations belonging to one organisation.
- **Users** — belong to one organisation, with roles: Requester, Approver.
- **Maintenance Requests** — raised against a site, with a description, estimated cost, and (once work is done) an actual cost.

---

## Functional requirements

1. **Authentication and authorisation.** Users log in. Roles govern what they can do. A Requester cannot approve. An Approver cannot approve their own request.
2. **Request lifecycle.** A request moves through states — at minimum: raised, pending approval, approved, completed, rejected. Not every transition is legal from every state. Enforce that.
3. **Threshold-based approval.** Each organisation has a configurable cost threshold. Requests below it skip approval. Requests above it require an Approver. You decide what happens when a request's actual cost later exceeds the threshold it was approved under — document your choice.
4. **Spend report.** An endpoint returning total spend per site over a date range, for the caller's organisation only.
5. **Audit trail.** Every state change and approval decision is recorded — who, what, when. Treat this as a compliance requirement.

---

## Frontend

Minimal. We are not evaluating your CSS.

Enough to log in, list requests, create one, and approve or reject one. Any framework, or server-rendered pages. Ugly is fine. Broken is not.

---

## Database

Set up your own — PostgreSQL, SQL Server, SQLite, your choice. We will ask why you picked it. Schema design and migrations are being evaluated.

---

## Security requirements

Treat this as a system handling client financial data. We expect to see and discuss:

- **Tenant isolation.** A user from Organisation A must not be able to read or modify anything belonging to Organisation B — including by manipulating IDs in requests. Be ready to explain where you enforced this and why at that layer.
- **Authorisation, enforced server-side.** Hiding a button is not authorisation.
- **Input validation** at your boundaries.
- **Secrets handling.** Nothing sensitive in source control. Explain what you did locally versus what you would do in production.
- **Audit integrity.** Who can modify the audit trail, and how do you prevent it?

You will be asked how you verified these, not just that you implemented them.

---

## What to submit

A Git repository containing the system, plus:

1. **README.md** — how to run it. We will run it. If it does not start in under 15 minutes on a clean machine, that is part of your result.
2. **DECISIONS.md** — one page is plenty. Your significant technical choices (architecture, data access, auth, project structure, key libraries), what you rejected, and the trade-off. Also list any ambiguity in this brief that you resolved by assumption.
3. **AI-LOG.md** — one page, honest.
   - What you delegated fully, what you delegated with tight constraints, and what you did by hand.
   - At least one of your actual task specifications or prompts.
   - One instance where the agent produced something plausible but wrong — what it was, how you caught it, and why it was easy to miss.

   There is no penalty for that last point. If you tell us the agent got everything right, we will assume you did not read the output carefully.
4. **Your agent configuration** — any `CLAUDE.md`, rules file, or equivalent. Commit it.
5. **Commit history** — commit as you actually work. Please do not squash into one commit; the history is evidence of process.
6. **A few tests** — not coverage. We want to see what you chose to test and why those things and not others.

---

## Explicitly out of scope

Visual design, deployment, file uploads, notifications, password reset, background jobs, exhaustive testing, performance tuning beyond sensible schema and query design.

If you catch yourself gold-plating, stop and note it in DECISIONS.md as something you would do next.

---

## How we will evaluate

| Area | What we are looking for |
|---|---|
| **AI-led execution** | How you planned, specified, and steered the work. Evidence you read and verified what came back. |
| **Architecture** | Sound structure, proportionate to the problem. Patterns chosen because they fit, not because they impress. |
| **Data model** | Schema quality, tenant isolation strategy, indexing judgment, migrations. |
| **Security** | Correctness of tenant isolation and authorisation, and the depth of your reasoning. |
| **Tooling choices** | Deliberate library and framework selection you can defend. |
| **Correctness** | State transitions enforced. The report returns the right numbers. |
| **Judgment** | What you chose not to build, and whether you can say why. |

> Over-engineering counts against you. So does an architecture you cannot explain.

---

## The review session

*(Max 40 minutes)*

- Brief walkthrough of what you built and why — **preferably a short 2-minute video can be shared before the interview.**
- We go deep on two or three areas — expect hard questions on tenant isolation and on the parts you delegated to the agent.
- A short live change request, using your normal AI workflow. It will be small; we are watching how you work, not whether you finish.

---

## Questions

If something is genuinely blocking, email **omar.waqar@bajcotechnologies.com**. If it is merely ambiguous, resolve it yourself and document the assumption — that is part of the exercise.
