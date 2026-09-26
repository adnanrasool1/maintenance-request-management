import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService, SESSION_KEY } from './auth.service';
import { errorInterceptor } from './error.interceptor';

const session = {
  accessToken: 'token-123',
  expiresAt: '2999-01-01T00:00:00Z',
  user: { id: 'u-1', email: 'bob@acme.example', role: 'Requester' },
};

describe('authInterceptor and errorInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    sessionStorage.clear();
    sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => {
    backend.verify();
    sessionStorage.clear();
  });

  it('adds the bearer token to /api calls', () => {
    http.get('/api/requests').subscribe();
    const req = backend.expectOne('/api/requests');
    expect(req.request.headers.get('Authorization')).toBe('Bearer token-123');
    req.flush([]);
  });

  it('does not add the token to the login call or to non-API URLs', () => {
    http.post('/api/auth/login', {}).subscribe();
    http.get('/assets/x.json').subscribe();
    const login = backend.expectOne('/api/auth/login');
    const asset = backend.expectOne('/assets/x.json');
    expect(login.request.headers.has('Authorization')).toBe(false);
    expect(asset.request.headers.has('Authorization')).toBe(false);
    login.flush({});
    asset.flush({});
  });

  it('sends no Authorization header when there is no session', () => {
    TestBed.inject(AuthService).logout();
    http.get('/api/sites').subscribe();
    const req = backend.expectOne('/api/sites');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
  });

  it('clears the session and goes to /login on a 401 from an API call', () => {
    let status = 0;
    http.get('/api/requests').subscribe({ error: (e) => (status = e.status) });
    backend.expectOne('/api/requests').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(status).toBe(401);
    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem(SESSION_KEY)).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('leaves a failed login alone: no logout and no redirect', () => {
    let status = 0;
    http.post('/api/auth/login', {}).subscribe({ error: (e) => (status = e.status) });
    backend
      .expectOne('/api/auth/login')
      .flush({ detail: 'Invalid email or password.' }, { status: 401, statusText: 'Unauthorized' });

    expect(status).toBe(401);
    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(true);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('does not log out on a 403', () => {
    http.post('/api/requests/x/approve', {}).subscribe({ error: () => undefined });
    backend
      .expectOne('/api/requests/x/approve')
      .flush(null, { status: 403, statusText: 'Forbidden' });

    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(true);
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
