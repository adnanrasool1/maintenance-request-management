import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SESSION_KEY } from '../../../core/auth.service';
import { RequestDetail, Role } from '../../../shared/api-models';
import { RequestDetailComponent } from './request-detail.component';

const ME = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const OTHER = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const ID = 'd0000001-0000-4000-8000-000000000001';

const pending: RequestDetail = {
  id: ID,
  siteId: '11111111-1111-4111-8111-111111111111',
  siteName: 'Head Office',
  raisedByUserId: OTHER,
  raisedByEmail: 'bob@acme.example',
  description: 'Replace AC unit.',
  estimatedCost: 6200,
  actualCost: null,
  status: 'PendingApproval',
  thresholdAtDecision: null,
  exceededThreshold: false,
  createdAt: '2026-09-26T10:15:30Z',
  completedAt: null,
};

describe('RequestDetailComponent', () => {
  let http: HttpTestingController;

  async function render(role: Role, request: RequestDetail) {
    sessionStorage.setItem(
      SESSION_KEY,
      JSON.stringify({
        accessToken: 't',
        expiresAt: '2999-01-01T00:00:00Z',
        user: { id: ME, email: 'alice@acme.example', role },
      }),
    );
    TestBed.configureTestingModule({
      imports: [RequestDetailComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(RequestDetailComponent);
    fixture.componentRef.setInput('id', ID);
    fixture.detectChanges();
    http.expectOne(`/api/requests/${ID}`).flush(request);
    await fixture.whenStable();
    return fixture;
  }

  function buttons(fixture: ComponentFixture<RequestDetailComponent>): string[] {
    const el = fixture.nativeElement as HTMLElement;
    return Array.from(el.querySelectorAll('button'), (b) => b.textContent!.trim());
  }

  beforeEach(() => sessionStorage.clear());
  afterEach(() => {
    http.verify();
    sessionStorage.clear();
  });

  describe('approve / reject visibility', () => {
    it('shows both buttons to an Approver on someone else’s pending request', async () => {
      expect(buttons(await render('Approver', pending))).toEqual(['Approve', 'Reject']);
    });

    it('hides them on the Approver’s own request', async () => {
      expect(buttons(await render('Approver', { ...pending, raisedByUserId: ME }))).toEqual([]);
    });

    it('hides them when the request is not pending', async () => {
      const approved = { ...pending, status: 'Approved' as const, thresholdAtDecision: 5000 };
      expect(buttons(await render('Approver', approved))).toEqual([]);
    });

    it('hides them from a Requester', async () => {
      expect(buttons(await render('Requester', pending))).toEqual([]);
    });
  });

  it('approves with the comment and shows the updated request', async () => {
    const fixture = await render('Approver', pending);
    fixture.componentInstance.comment.setValue('  Go ahead  ');
    fixture.componentInstance.decide('approve');

    const req = http.expectOne(`/api/requests/${ID}/approve`);
    expect(req.request.body).toEqual({ comment: 'Go ahead' });
    req.flush({ ...pending, status: 'Approved', thresholdAtDecision: 5000 });
    await fixture.whenStable();

    expect(buttons(fixture)).toEqual([]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Approved');
  });

  it('sends a null comment when none is given', async () => {
    const fixture = await render('Approver', pending);
    fixture.componentInstance.decide('reject');
    const req = http.expectOne(`/api/requests/${ID}/reject`);
    expect(req.request.body).toEqual({ comment: null });
    req.flush({ ...pending, status: 'Rejected' });
  });

  it('on 409 shows a message and reloads the latest version', async () => {
    const fixture = await render('Approver', pending);
    fixture.componentInstance.decide('approve');
    http
      .expectOne(`/api/requests/${ID}/approve`)
      .flush({ status: 409 }, { status: 409, statusText: 'Conflict' });
    http.expectOne(`/api/requests/${ID}`).flush({ ...pending, status: 'Rejected' });
    await fixture.whenStable();

    expect(fixture.componentInstance.actionError()).toContain('already decided');
    expect(buttons(fixture)).toEqual([]);
  });

  it('on 403 shows the server’s reason', async () => {
    const fixture = await render('Approver', pending);
    fixture.componentInstance.decide('reject');
    http
      .expectOne(`/api/requests/${ID}/reject`)
      .flush(
        { status: 403, detail: 'You cannot approve or reject a request you raised.' },
        { status: 403, statusText: 'Forbidden' },
      );
    expect(fixture.componentInstance.actionError()).toBe(
      'You cannot approve or reject a request you raised.',
    );
  });

  it('does not send an over-long comment', async () => {
    const fixture = await render('Approver', pending);
    fixture.componentInstance.comment.setValue('x'.repeat(2001));
    fixture.componentInstance.decide('approve');
    http.expectNone(`/api/requests/${ID}/approve`);
  });
});
