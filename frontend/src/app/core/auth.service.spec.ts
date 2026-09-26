import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LoginResponse } from '../shared/api-models';
import { AuthService, SESSION_KEY } from './auth.service';

const response: LoginResponse = {
  accessToken: 'token-123',
  expiresAt: '2026-09-26T11:00:00Z',
  user: { id: 'u-1', email: 'alice@acme.example', role: 'Approver' },
};

function setup(): { auth: AuthService; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  return { auth: TestBed.inject(AuthService), http: TestBed.inject(HttpTestingController) };
}

describe('AuthService', () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-09-26T10:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
    sessionStorage.clear();
  });

  it('stores the token and user in sessionStorage after login and exposes them as signals', () => {
    const { auth, http } = setup();
    let user: unknown;
    auth.login({ email: 'alice@acme.example', password: 'pw' }).subscribe((u) => (user = u));

    const req = http.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'alice@acme.example', password: 'pw' });
    req.flush(response);

    expect(user).toEqual(response.user);
    expect(JSON.parse(sessionStorage.getItem(SESSION_KEY)!)).toEqual(response);
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.currentUser()).toEqual(response.user);
    expect(auth.role()).toBe('Approver');
    expect(auth.token()).toBe('token-123');
    http.verify();
  });

  it('restores a stored session on start-up', () => {
    sessionStorage.setItem(SESSION_KEY, JSON.stringify(response));
    const { auth } = setup();
    expect(auth.currentUser()).toEqual(response.user);
    expect(auth.token()).toBe('token-123');
  });

  it('clears sessionStorage and the signals on logout', () => {
    sessionStorage.setItem(SESSION_KEY, JSON.stringify(response));
    const { auth } = setup();
    auth.logout();
    expect(sessionStorage.getItem(SESSION_KEY)).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.currentUser()).toBeNull();
    expect(auth.token()).toBeNull();
  });

  it('ignores an expired stored session', () => {
    sessionStorage.setItem(
      SESSION_KEY,
      JSON.stringify({ ...response, expiresAt: '2026-09-26T09:59:59Z' }),
    );
    const { auth } = setup();
    expect(auth.isAuthenticated()).toBe(false);
    expect(auth.token()).toBeNull();
  });

  it('drops the session once expiresAt has passed', () => {
    sessionStorage.setItem(SESSION_KEY, JSON.stringify(response));
    const { auth } = setup();
    expect(auth.hasValidSession()).toBe(true);

    vi.setSystemTime(new Date('2026-09-26T11:00:00Z'));

    expect(auth.token()).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem(SESSION_KEY)).toBeNull();
  });

  it('ignores malformed stored data', () => {
    sessionStorage.setItem(SESSION_KEY, '{not json');
    const { auth } = setup();
    expect(auth.isAuthenticated()).toBe(false);
  });
});
