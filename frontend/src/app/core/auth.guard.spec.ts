import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { RequestDetail, User } from '../shared/api-models';
import { authGuard, canDecide } from './auth.guard';
import { SESSION_KEY } from './auth.service';

function runGuard(): ReturnType<typeof authGuard> {
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient()] });
  return TestBed.runInInjectionContext(() =>
    authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
  );
}

describe('authGuard', () => {
  const user: User = { id: 'u-1', email: 'bob@acme.example', role: 'Requester' };

  beforeEach(() => sessionStorage.clear());
  afterEach(() => sessionStorage.clear());

  it('allows navigation with a valid session', () => {
    sessionStorage.setItem(
      SESSION_KEY,
      JSON.stringify({ accessToken: 't', expiresAt: '2999-01-01T00:00:00Z', user }),
    );
    expect(runGuard()).toBe(true);
  });

  it('redirects to /login without a session', () => {
    const result = runGuard();
    expect(result instanceof UrlTree).toBe(true);
    expect(String(result)).toBe('/login');
  });

  it('redirects to /login when the session has expired', () => {
    sessionStorage.setItem(
      SESSION_KEY,
      JSON.stringify({ accessToken: 't', expiresAt: '2000-01-01T00:00:00Z', user }),
    );
    expect(String(runGuard())).toBe('/login');
  });
});

describe('canDecide', () => {
  const approver: User = { id: 'AAAA-1', email: 'alice@acme.example', role: 'Approver' };
  const pending = {
    status: 'PendingApproval',
    raisedByUserId: 'bbbb-2',
  } as RequestDetail;

  it('is true for an Approver on someone else’s pending request', () => {
    expect(canDecide(approver, pending)).toBe(true);
  });

  it('is false on the Approver’s own request, even if the ID case differs', () => {
    expect(canDecide(approver, { ...pending, raisedByUserId: 'aaaa-1' })).toBe(false);
  });

  it('is false when the request is not pending', () => {
    for (const status of ['Raised', 'Approved', 'Rejected', 'Completed'] as const) {
      expect(canDecide(approver, { ...pending, status })).toBe(false);
    }
  });

  it('is false for a Requester, and when there is no user or request', () => {
    expect(canDecide({ ...approver, role: 'Requester' }, pending)).toBe(false);
    expect(canDecide(null, pending)).toBe(false);
    expect(canDecide(approver, null)).toBe(false);
  });
});
