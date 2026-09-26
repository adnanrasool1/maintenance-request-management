import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let http: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
  });

  afterEach(() => sessionStorage.clear());

  function submit(email: string, password: string) {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.componentInstance.form.setValue({ email, password });
    fixture.componentInstance.submit();
    return fixture;
  }

  it('does not call the API when a field is empty', () => {
    const fixture = submit('', '');
    http.expectNone('/api/auth/login');
    expect(fixture.componentInstance.form.controls.email.touched).toBe(true);
  });

  it('shows one generic message on 401', async () => {
    const fixture = submit('bob@acme.example', 'wrong');
    http
      .expectOne('/api/auth/login')
      .flush({ detail: 'Invalid email or password.' }, { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();

    const alert = (fixture.nativeElement as HTMLElement).querySelector('[role=alert]');
    expect(alert?.textContent?.trim()).toBe('Invalid email or password.');
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('goes to the request list after a successful login', () => {
    submit('bob@acme.example', 'password');
    http.expectOne('/api/auth/login').flush({
      accessToken: 't',
      expiresAt: '2999-01-01T00:00:00Z',
      user: { id: 'u-1', email: 'bob@acme.example', role: 'Requester' },
    });
    expect(router.navigate).toHaveBeenCalledWith(['/requests']);
  });

  it('refuses admin roles, which have no screens in this UI', () => {
    const fixture = submit('admin@acme.example', 'password');
    http.expectOne('/api/auth/login').flush({
      accessToken: 't',
      expiresAt: '2999-01-01T00:00:00Z',
      user: { id: 'u-9', email: 'admin@acme.example', role: 'TenantAdmin' },
    });
    expect(router.navigate).not.toHaveBeenCalled();
    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(false);
    expect(fixture.componentInstance.error()).toContain('Requesters and Approvers');
  });
});
