import {
  HttpErrorResponse,
  HttpEvent,
  HttpHeaders,
  HttpInterceptorFn,
  HttpRequest,
  HttpResponse,
} from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import {
  CreateRequest,
  DecisionRequest,
  LoginRequest,
  LoginResponse,
  ProblemDetails,
  REQUEST_STATUSES,
  RequestDetail,
  RequestSummary,
  Role,
  Site,
  User,
} from '../shared/api-models';
import { MAX_COST, MAX_TEXT_LENGTH, hasAtMostTwoDecimals } from '../shared/validation';
import { API_PREFIX, LOGIN_URL } from './api.service';

/*
 * In-browser mock of the API, used only when `environment.useMocks` is true (development build).
 * It sits at the end of the interceptor chain, so the real ApiService, auth interceptor and
 * error interceptor all run. It follows docs/api-contract.md closely enough to exercise the UI:
 * threshold routing, Requester visibility, 403 self-approval, 409 non-pending, 404 unknown IDs.
 *
 * MOCK DATA ONLY: one organisation, two sites, one Requester and two Approvers.
 * Every mock user's password is "password". These are not real credentials.
 */

interface MockUser extends User {
  password: string;
}

const THRESHOLD = 5000;
const TOKEN_PREFIX = 'mock-token.';
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const REQUESTER_ROLES: readonly Role[] = ['Requester', 'Approver'];

export const MOCK_SITES: readonly Site[] = [
  { id: '11111111-1111-4111-8111-111111111111', name: 'Head Office' },
  { id: '22222222-2222-4222-8222-222222222222', name: 'Warehouse North' },
];

export const MOCK_USERS: readonly MockUser[] = [
  {
    id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
    email: 'bob@acme.example',
    role: 'Requester',
    password: 'password',
  },
  {
    id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
    email: 'alice@acme.example',
    role: 'Approver',
    password: 'password',
  },
  {
    id: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
    email: 'carol@acme.example',
    role: 'Approver',
    password: 'password',
  },
];

const [BOB, ALICE] = MOCK_USERS;

function seedRequests(): RequestDetail[] {
  const base = {
    actualCost: null,
    exceededThreshold: false,
    completedAt: null,
  };
  return [
    {
      ...base,
      id: 'd0000001-0000-4000-8000-000000000001',
      siteId: MOCK_SITES[0].id,
      siteName: MOCK_SITES[0].name,
      raisedByUserId: BOB.id,
      raisedByEmail: BOB.email,
      description: 'Replace broken air-conditioning unit on floor 3.',
      estimatedCost: 6200,
      status: 'PendingApproval',
      thresholdAtDecision: null,
      createdAt: '2026-09-26T10:15:30Z',
    },
    {
      ...base,
      id: 'd0000002-0000-4000-8000-000000000002',
      siteId: MOCK_SITES[1].id,
      siteName: MOCK_SITES[1].name,
      raisedByUserId: BOB.id,
      raisedByEmail: BOB.email,
      description: 'Fix leaking tap in staff kitchen.',
      estimatedCost: 120,
      status: 'Approved',
      thresholdAtDecision: THRESHOLD,
      createdAt: '2026-09-25T08:02:11Z',
    },
    {
      ...base,
      id: 'd0000003-0000-4000-8000-000000000003',
      siteId: MOCK_SITES[1].id,
      siteName: MOCK_SITES[1].name,
      raisedByUserId: ALICE.id,
      raisedByEmail: ALICE.email,
      description: 'Resurface the loading bay floor.',
      estimatedCost: 18000,
      status: 'PendingApproval',
      thresholdAtDecision: null,
      createdAt: '2026-09-24T14:40:00Z',
    },
  ];
}

/** GUID v4 without `crypto.randomUUID`, which is missing outside secure contexts. */
function newGuid(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

function sameId(a: string, b: string): boolean {
  return a.toLowerCase() === b.toLowerCase();
}

function toSummary(r: RequestDetail): RequestSummary {
  return {
    id: r.id,
    siteId: r.siteId,
    siteName: r.siteName,
    description: r.description,
    estimatedCost: r.estimatedCost,
    status: r.status,
    raisedByUserId: r.raisedByUserId,
    createdAt: r.createdAt,
  };
}

const TITLES: Record<number, string> = {
  400: 'One or more validation errors occurred.',
  401: 'Unauthorized',
  403: 'Forbidden',
  404: 'Not Found',
  409: 'Conflict',
};

function ok<T>(req: HttpRequest<unknown>, body: T, status = 200, headers?: HttpHeaders) {
  return of(new HttpResponse<T>({ status, body, headers, url: req.url }));
}

function fail(
  req: HttpRequest<unknown>,
  status: number,
  detail?: string,
  errors?: Record<string, string[]>,
): Observable<never> {
  const error: ProblemDetails = { title: TITLES[status], status, detail, errors };
  return throwError(
    () => new HttpErrorResponse({ status, statusText: TITLES[status], error, url: req.url }),
  );
}

@Injectable({ providedIn: 'root' })
export class MockBackend {
  private readonly requests = seedRequests();

  /** Answers an `/api/...` request, or returns null for anything else. */
  handle(req: HttpRequest<unknown>): Observable<HttpEvent<unknown>> | null {
    const path = req.url;
    if (!path.startsWith(API_PREFIX)) {
      return null;
    }
    if (req.method === 'POST' && path === LOGIN_URL) {
      return this.login(req, (req.body ?? {}) as Partial<LoginRequest>);
    }

    const caller = this.caller(req);
    if (!caller) {
      return fail(req, 401);
    }

    if (req.method === 'GET' && path === '/api/sites') {
      return ok(
        req,
        [...MOCK_SITES].sort((a, b) => a.name.localeCompare(b.name)),
      );
    }
    if (path === '/api/requests') {
      if (req.method === 'GET') return this.list(req, caller);
      if (req.method === 'POST') return this.create(req, caller);
    }
    const match = /^\/api\/requests\/([^/]+)(?:\/(approve|reject))?$/.exec(path);
    if (match) {
      const [, id, action] = match;
      if (req.method === 'GET' && !action) return this.detail(req, caller, id);
      if (req.method === 'POST' && action) {
        return this.decide(req, caller, id, action as 'approve' | 'reject');
      }
    }
    return fail(req, 404);
  }

  private login(req: HttpRequest<unknown>, body: Partial<LoginRequest>) {
    const errors: Record<string, string[]> = {};
    if (!body.email?.trim()) errors['email'] = ['Email is required.'];
    if (!body.password) errors['password'] = ['Password is required.'];
    if (Object.keys(errors).length) return fail(req, 400, undefined, errors);

    const email = body.email!.trim().toLowerCase();
    const user = MOCK_USERS.find((u) => u.email === email && u.password === body.password);
    if (!user) return fail(req, 401, 'Invalid email or password.');

    const response: LoginResponse = {
      accessToken: TOKEN_PREFIX + user.id,
      expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      user: { id: user.id, email: user.email, role: user.role },
    };
    return ok(req, response);
  }

  private caller(req: HttpRequest<unknown>): MockUser | undefined {
    const header = req.headers.get('Authorization') ?? '';
    const prefix = `Bearer ${TOKEN_PREFIX}`;
    if (!header.startsWith(prefix)) return undefined;
    const id = header.slice(prefix.length);
    return MOCK_USERS.find((u) => u.id === id);
  }

  private visibleTo(caller: MockUser): RequestDetail[] {
    return caller.role === 'Approver'
      ? this.requests
      : this.requests.filter((r) => sameId(r.raisedByUserId, caller.id));
  }

  private list(req: HttpRequest<unknown>, caller: MockUser) {
    if (!REQUESTER_ROLES.includes(caller.role)) return fail(req, 403);
    const raw = req.params.get('status');
    const status = raw === null ? null : REQUEST_STATUSES.find((s) => sameId(s, raw));
    if (status === undefined) {
      return fail(req, 400, undefined, { status: ['Status is not a valid value.'] });
    }
    const items = this.visibleTo(caller)
      .filter((r) => status === null || r.status === status)
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
      .map(toSummary);
    return ok(req, items);
  }

  private find(caller: MockUser, id: string): RequestDetail | undefined {
    if (!GUID.test(id)) return undefined;
    return this.visibleTo(caller).find((r) => sameId(r.id, id));
  }

  private detail(req: HttpRequest<unknown>, caller: MockUser, id: string) {
    if (!REQUESTER_ROLES.includes(caller.role)) return fail(req, 403);
    const found = this.find(caller, id);
    return found ? ok(req, { ...found }) : fail(req, 404, 'The requested resource was not found.');
  }

  private create(req: HttpRequest<unknown>, caller: MockUser) {
    if (!REQUESTER_ROLES.includes(caller.role)) return fail(req, 403);
    const body = (req.body ?? {}) as Partial<CreateRequest>;
    const errors: Record<string, string[]> = {};
    if (typeof body.siteId !== 'string' || !GUID.test(body.siteId)) {
      errors['siteId'] = ['Site is required.'];
    }
    if (typeof body.description !== 'string' || !body.description.trim()) {
      errors['description'] = ['Description is required.'];
    } else if (body.description.length > MAX_TEXT_LENGTH) {
      errors['description'] = [`Description must be at most ${MAX_TEXT_LENGTH} characters.`];
    }
    const cost = body.estimatedCost;
    if (typeof cost !== 'number' || !(cost > 0)) {
      errors['estimatedCost'] = ['Estimated cost must be greater than 0.'];
    } else if (cost > MAX_COST || !hasAtMostTwoDecimals(cost)) {
      errors['estimatedCost'] = ['Estimated cost must be at most 1,000,000.00 with 2 decimals.'];
    }
    if (Object.keys(errors).length) return fail(req, 400, undefined, errors);

    const site = MOCK_SITES.find((s) => sameId(s.id, body.siteId!));
    if (!site) return fail(req, 404, 'The requested resource was not found.');

    const needsApproval = cost! >= THRESHOLD;
    const created: RequestDetail = {
      id: newGuid(),
      siteId: site.id,
      siteName: site.name,
      raisedByUserId: caller.id,
      raisedByEmail: caller.email,
      description: body.description!,
      estimatedCost: cost!,
      actualCost: null,
      status: needsApproval ? 'PendingApproval' : 'Approved',
      thresholdAtDecision: needsApproval ? null : THRESHOLD,
      exceededThreshold: false,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    this.requests.push(created);
    const headers = new HttpHeaders({ Location: `/api/requests/${created.id}` });
    return ok(req, { ...created }, 201, headers);
  }

  private decide(
    req: HttpRequest<unknown>,
    caller: MockUser,
    id: string,
    action: 'approve' | 'reject',
  ) {
    if (caller.role !== 'Approver') return fail(req, 403);
    const comment = ((req.body ?? {}) as DecisionRequest).comment;
    if (comment != null && (typeof comment !== 'string' || comment.length > MAX_TEXT_LENGTH)) {
      return fail(req, 400, undefined, {
        comment: [`Comment must be at most ${MAX_TEXT_LENGTH} characters.`],
      });
    }
    const found = this.find(caller, id);
    if (!found) return fail(req, 404, 'The requested resource was not found.');
    if (sameId(found.raisedByUserId, caller.id)) {
      return fail(req, 403, 'You cannot approve or reject a request you raised.');
    }
    if (found.status !== 'PendingApproval') {
      return fail(req, 409, 'The request is not in a state that allows this action.');
    }
    found.status = action === 'approve' ? 'Approved' : 'Rejected';
    found.thresholdAtDecision = action === 'approve' ? THRESHOLD : null;
    return ok(req, { ...found });
  }
}

/** Last interceptor in the chain when mocks are on: answers `/api` calls from MockBackend. */
export const mockApiInterceptor: HttpInterceptorFn = (req, next) =>
  inject(MockBackend).handle(req) ?? next(req);
