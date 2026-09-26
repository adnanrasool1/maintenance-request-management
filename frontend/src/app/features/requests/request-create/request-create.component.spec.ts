import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RequestCreateComponent } from './request-create.component';

const SITE_ID = '11111111-1111-4111-8111-111111111111';

describe('RequestCreateComponent', () => {
  let http: HttpTestingController;
  let router: Router;

  function create() {
    TestBed.configureTestingModule({
      imports: [RequestCreateComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(RequestCreateComponent);
    http.expectOne('/api/sites').flush([{ id: SITE_ID, name: 'Head Office' }]);
    return fixture;
  }

  afterEach(() => http.verify());

  describe('validation', () => {
    it('requires every field', () => {
      const form = create().componentInstance.form;
      expect(form.controls.siteId.hasError('required')).toBe(true);
      expect(form.controls.description.hasError('required')).toBe(true);
      expect(form.controls.estimatedCost.hasError('required')).toBe(true);
    });

    it('rejects a blank or over-long description', () => {
      const description = create().componentInstance.form.controls.description;
      description.setValue('   ');
      expect(description.hasError('blank')).toBe(true);
      description.setValue('x'.repeat(2001));
      expect(description.hasError('maxlength')).toBe(true);
      description.setValue('x'.repeat(2000));
      expect(description.valid).toBe(true);
    });

    it('mirrors the cost rules: > 0, at most 2 decimals, at most 1,000,000.00', () => {
      const estimatedCost = create().componentInstance.form.controls.estimatedCost;
      const errorsFor = (value: number) => {
        estimatedCost.setValue(value);
        return estimatedCost.errors;
      };
      expect(errorsFor(0)).toEqual({ positive: true });
      expect(errorsFor(-5)).toEqual({ positive: true });
      expect(errorsFor(1.234)).toEqual({ decimals: true });
      expect(errorsFor(1_000_000.01)).toEqual({ maxCost: true });
      expect(errorsFor(0.01)).toBeNull();
      expect(errorsFor(1250.5)).toBeNull();
      expect(errorsFor(1_000_000)).toBeNull();
    });

    it('does not call the API while the form is invalid', () => {
      create().componentInstance.submit();
      http.expectNone('/api/requests');
    });
  });

  it('posts the contract body and opens the new request', () => {
    const component = create().componentInstance;
    component.form.setValue({ siteId: SITE_ID, description: 'Fix tap', estimatedCost: 120.5 });
    component.submit();

    const req = http.expectOne('/api/requests');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      siteId: SITE_ID,
      description: 'Fix tap',
      estimatedCost: 120.5,
    });
    req.flush({ id: 'new-id' }, { status: 201, statusText: 'Created' });
    expect(router.navigate).toHaveBeenCalledWith(['/requests', 'new-id']);
  });

  it('shows server 400 errors next to their fields', async () => {
    const fixture = create();
    const component = fixture.componentInstance;
    component.form.setValue({ siteId: SITE_ID, description: 'Fix tap', estimatedCost: 10 });
    component.submit();
    http
      .expectOne('/api/requests')
      .flush(
        { status: 400, errors: { description: ['Description is required.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    await fixture.whenStable();

    expect(component.serverErrors().description).toEqual(['Description is required.']);
    expect(component.error()).toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Description is required.',
    );
  });

  it('shows a 404 for the site as a general error', () => {
    const component = create().componentInstance;
    component.form.setValue({ siteId: SITE_ID, description: 'Fix tap', estimatedCost: 10 });
    component.submit();
    http.expectOne('/api/requests').flush(null, { status: 404, statusText: 'Not Found' });
    expect(component.error()).toContain('site no longer exists');
  });
});
