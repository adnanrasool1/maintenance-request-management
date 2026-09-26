import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/api.service';
import { Site } from '../../../shared/api-models';
import { problemOf, statusOf } from '../../../shared/problem';
import { MAX_TEXT_LENGTH, cost, notBlank } from '../../../shared/validation';

const FIELDS = ['siteId', 'description', 'estimatedCost'] as const;
type Field = (typeof FIELDS)[number];

@Component({
  selector: 'app-request-create',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './request-create.component.html',
})
export class RequestCreateComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly maxLength = MAX_TEXT_LENGTH;
  readonly form = inject(FormBuilder).group({
    siteId: ['', Validators.required],
    description: ['', [Validators.required, notBlank, Validators.maxLength(MAX_TEXT_LENGTH)]],
    estimatedCost: [null as number | null, [Validators.required, cost]],
  });

  readonly sites = signal<Site[]>([]);
  readonly sitesLoaded = signal(false);
  readonly submitting = signal(false);
  /** Server 400 messages per field (contract §4.1). */
  readonly serverErrors = signal<Partial<Record<Field, string[]>>>({});
  readonly error = signal<string | null>(null);

  constructor() {
    this.api.getSites().subscribe({
      next: (sites) => {
        this.sites.set(sites);
        this.sitesLoaded.set(true);
      },
      error: () => this.error.set('Could not load sites. Please try again.'),
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { siteId, description, estimatedCost } = this.form.getRawValue();
    this.submitting.set(true);
    this.serverErrors.set({});
    this.error.set(null);
    this.api
      .createRequest({ siteId: siteId!, description: description!, estimatedCost: estimatedCost! })
      .subscribe({
        next: (created) => void this.router.navigate(['/requests', created.id]),
        error: (err: unknown) => {
          this.submitting.set(false);
          this.handleError(err);
        },
      });
  }

  private handleError(err: unknown): void {
    switch (statusOf(err)) {
      case 400: {
        const errors = problemOf(err)?.errors ?? {};
        const byField: Partial<Record<Field, string[]>> = {};
        const other: string[] = [];
        for (const [key, messages] of Object.entries(errors)) {
          if ((FIELDS as readonly string[]).includes(key)) byField[key as Field] = messages;
          else other.push(...messages);
        }
        this.serverErrors.set(byField);
        const hasFieldErrors = Object.keys(byField).length > 0;
        this.error.set(
          other.length
            ? other.join(' ')
            : hasFieldErrors
              ? null
              : 'The request is not valid. Please check the form.',
        );
        return;
      }
      case 404:
        this.error.set('The selected site no longer exists. Please choose another site.');
        return;
      case 403:
        this.error.set('Your role cannot raise maintenance requests.');
        return;
      default:
        this.error.set('Could not create the request. Please try again.');
    }
  }
}
