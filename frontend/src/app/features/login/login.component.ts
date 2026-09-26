import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { statusOf } from '../../shared/problem';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html',
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', Validators.required],
    password: ['', Validators.required],
  });
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    this.auth.login(this.form.getRawValue()).subscribe({
      next: (user) => {
        this.submitting.set(false);
        if (user.role !== 'Requester' && user.role !== 'Approver') {
          // Admin actions are API-only (PRD §8); this UI has nothing for these roles.
          this.auth.logout();
          this.error.set('This app is for Requesters and Approvers. Admin tasks use the API.');
          return;
        }
        void this.router.navigate(['/requests']);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        // One generic message for a wrong email or password (contract §4.3).
        this.error.set(
          statusOf(err) === 401 || statusOf(err) === 400
            ? 'Invalid email or password.'
            : 'Sign-in failed. Please try again.',
        );
      },
    });
  }
}
