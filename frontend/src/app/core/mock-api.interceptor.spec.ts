import {
  HttpErrorResponse,
  HttpInterceptorFn,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { ApiService } from './api.service';
import { MOCK_SITES, mockApiInterceptor } from './mock-api.interceptor';

async function statusOf(promise: Promise<unknown>): Promise<number> {
  try {
    await promise;
    return 200;
  } catch (e) {
    return (e as HttpErrorResponse).status;
  }
}

describe('mockApiInterceptor', () => {
  let token = '';
  // Stand-in for the auth interceptor, so this spec only tests the mock.
  const bearer: HttpInterceptorFn = (req, next) =>
    next(token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req);

  beforeEach(() => {
    token = '';
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([bearer, mockApiInterceptor]))],
    });
  });

  const login = async (email: string) => {
    const api = TestBed.inject(ApiService);
    token = (await firstValueFrom(api.login({ email, password: 'password' }))).accessToken;
    return api;
  };

  it('rejects a wrong password with 401 and no token with 401', async () => {
    const api = TestBed.inject(ApiService);
    expect(
      await statusOf(firstValueFrom(api.login({ email: 'bob@acme.example', password: 'x' }))),
    ).toBe(401);
    expect(await statusOf(firstValueFrom(api.getRequests()))).toBe(401);
  });

  it('routes below the threshold to Approved and at the threshold to PendingApproval', async () => {
    const api = await login('bob@acme.example');
    const siteId = MOCK_SITES[0].id;
    const below = await firstValueFrom(
      api.createRequest({ siteId, description: 'a', estimatedCost: 4999.99 }),
    );
    const at = await firstValueFrom(
      api.createRequest({ siteId, description: 'b', estimatedCost: 5000 }),
    );
    expect(below.status).toBe('Approved');
    expect(at.status).toBe('PendingApproval');
  });

  it('shows a Requester only their own requests', async () => {
    const api = await login('bob@acme.example');
    const mine = await firstValueFrom(api.getRequests());
    expect(mine.length).toBeGreaterThan(0);
    expect(mine.every((r) => r.raisedByUserId === 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa')).toBe(
      true,
    );
    // Alice's request is invisible to Bob: 404, not 403.
    expect(
      await statusOf(firstValueFrom(api.getRequest('d0000003-0000-4000-8000-000000000003'))),
    ).toBe(404);
  });

  it('returns 403 on self-approval, 409 on a decided request and 404 on an unknown ID', async () => {
    const api = await login('alice@acme.example');
    expect(
      await statusOf(firstValueFrom(api.approve('d0000003-0000-4000-8000-000000000003', {}))),
    ).toBe(403);
    expect(
      await statusOf(firstValueFrom(api.approve('d0000002-0000-4000-8000-000000000002', {}))),
    ).toBe(409);
    expect(
      await statusOf(firstValueFrom(api.approve('d0000009-0000-4000-8000-000000000009', {}))),
    ).toBe(404);
    expect(await statusOf(firstValueFrom(api.getRequest('not-a-guid')))).toBe(404);
  });

  it('lets another Approver approve, and returns 404 for an unknown site', async () => {
    const api = await login('carol@acme.example');
    const approved = await firstValueFrom(
      api.approve('d0000003-0000-4000-8000-000000000003', { comment: 'ok' }),
    );
    expect(approved.status).toBe('Approved');
    expect(approved.thresholdAtDecision).toBe(5000);
    const unknownSite = api.createRequest({
      siteId: '99999999-9999-4999-8999-999999999999',
      description: 'x',
      estimatedCost: 10,
    });
    expect(await statusOf(firstValueFrom(unknownSite))).toBe(404);
  });
});
