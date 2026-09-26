import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SESSION_KEY } from '../../../core/auth.service';
import { Role } from '../../../shared/api-models';
import { RequestListComponent } from './request-list.component';

function signIn(role: Role): void {
  sessionStorage.setItem(
    SESSION_KEY,
    JSON.stringify({
      accessToken: 't',
      expiresAt: '2999-01-01T00:00:00Z',
      user: { id: 'u-1', email: 'x@acme.example', role },
    }),
  );
}

describe('RequestListComponent', () => {
  let http: HttpTestingController;

  function create() {
    TestBed.configureTestingModule({
      imports: [RequestListComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.createComponent(RequestListComponent);
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => {
    http.verify();
    sessionStorage.clear();
  });

  it('shows no status filter to a Requester', async () => {
    signIn('Requester');
    const fixture = create();
    http.expectOne('/api/requests').flush([]);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).querySelector('select')).toBeNull();
  });

  it('lets an Approver filter by status with ?status=', async () => {
    signIn('Approver');
    const fixture = create();
    http.expectOne('/api/requests').flush([]);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('option').length).toBe(6);

    fixture.componentInstance.onStatusChange('PendingApproval');
    const req = http.expectOne((r) => r.url === '/api/requests');
    expect(req.request.params.get('status')).toBe('PendingApproval');
    req.flush([]);
  });
});
