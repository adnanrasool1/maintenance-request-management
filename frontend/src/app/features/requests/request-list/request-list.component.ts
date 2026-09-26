import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { ApiService } from '../../../core/api.service';
import { AuthService } from '../../../core/auth.service';
import { REQUEST_STATUSES, RequestStatus, RequestSummary } from '../../../shared/api-models';
import { statusOf } from '../../../shared/problem';
import { statusLabel } from '../../../shared/status-label';

@Component({
  selector: 'app-request-list',
  imports: [RouterLink, DatePipe, DecimalPipe],
  templateUrl: './request-list.component.html',
})
export class RequestListComponent {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private loadSub?: Subscription;

  readonly statuses = REQUEST_STATUSES;
  readonly statusLabel = statusLabel;
  /** Only Approvers see the whole organisation, so only they get the status filter (FR-5.2). */
  readonly isApprover = computed(() => this.auth.role() === 'Approver');
  readonly canCreate = computed(() => {
    const role = this.auth.role();
    return role === 'Requester' || role === 'Approver';
  });

  readonly status = signal<RequestStatus | ''>('');
  readonly requests = signal<RequestSummary[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    this.load();
  }

  onStatusChange(value: string): void {
    this.status.set(value as RequestStatus | '');
    this.load();
  }

  private load(): void {
    // Cancel an earlier call so a slow response can't overwrite a newer filter's results.
    this.loadSub?.unsubscribe();
    this.loading.set(true);
    this.error.set(null);
    this.loadSub = this.api.getRequests(this.status() || undefined).subscribe({
      next: (items) => {
        this.requests.set(items);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.requests.set([]);
        this.loading.set(false);
        this.error.set(
          statusOf(err) === 403
            ? 'Your role cannot view maintenance requests.'
            : 'Could not load requests. Please try again.',
        );
      },
    });
  }
}
