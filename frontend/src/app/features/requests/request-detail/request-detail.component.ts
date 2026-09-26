import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../../core/api.service';
import { canDecide } from '../../../core/auth.guard';
import { AuthService } from '../../../core/auth.service';
import { RequestDetail } from '../../../shared/api-models';
import { problemOf, statusOf } from '../../../shared/problem';
import { statusLabel } from '../../../shared/status-label';
import { MAX_TEXT_LENGTH } from '../../../shared/validation';

type Decision = 'approve' | 'reject';

@Component({
  selector: 'app-request-detail',
  imports: [ReactiveFormsModule, RouterLink, DatePipe, DecimalPipe],
  templateUrl: './request-detail.component.html',
})
export class RequestDetailComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);

  /** Route parameter `:id`, bound by `withComponentInputBinding()`. */
  readonly id = input.required<string>();

  readonly statusLabel = statusLabel;
  readonly maxLength = MAX_TEXT_LENGTH;
  readonly request = signal<RequestDetail | null>(null);
  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly deciding = signal<Decision | null>(null);
  readonly comment = new FormControl('', {
    nonNullable: true,
    validators: [Validators.maxLength(MAX_TEXT_LENGTH)],
  });

  /** UX only: the server rejects decisions by non-Approvers, on own requests and on non-pending ones. */
  readonly canDecide = computed(() => canDecide(this.auth.currentUser(), this.request()));

  ngOnInit(): void {
    this.load();
  }

  decide(decision: Decision): void {
    if (this.deciding() || this.comment.invalid) {
      return;
    }
    const text = this.comment.value.trim();
    const body = { comment: text ? text : null };
    const call =
      decision === 'approve' ? this.api.approve(this.id(), body) : this.api.reject(this.id(), body);

    this.deciding.set(decision);
    this.actionError.set(null);
    call.subscribe({
      next: (updated) => {
        this.request.set(updated);
        this.comment.reset();
        this.deciding.set(null);
      },
      error: (err: unknown) => {
        this.deciding.set(null);
        this.handleDecisionError(err);
      },
    });
  }

  private load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.api.getRequest(this.id()).subscribe({
      next: (request) => {
        this.request.set(request);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.request.set(null);
        this.loading.set(false);
        this.loadError.set(
          statusOf(err) === 404
            ? 'Request not found.'
            : 'Could not load the request. Please try again.',
        );
      },
    });
  }

  private handleDecisionError(err: unknown): void {
    switch (statusOf(err)) {
      case 400:
        this.actionError.set(
          problemOf(err)?.errors?.['comment']?.join(' ') ?? 'The comment is not valid.',
        );
        return;
      case 403:
        this.actionError.set(
          problemOf(err)?.detail ?? 'You are not allowed to approve or reject this request.',
        );
        return;
      case 404:
        this.actionError.set('This request no longer exists.');
        return;
      case 409:
        // Someone else decided first, or the request isn't pending any more: show the latest.
        this.actionError.set(
          'This request was already decided or changed by someone else. The latest version is shown.',
        );
        this.load();
        return;
      default:
        this.actionError.set('Could not save the decision. Please try again.');
    }
  }
}
