import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, map, tap } from 'rxjs';
import { LoginRequest, LoginResponse, User } from '../shared/api-models';
import { ApiService } from './api.service';

/** The only sessionStorage key the app uses. Nothing else may touch sessionStorage. */
export const SESSION_KEY = 'mra.session';

type Session = LoginResponse;

function isExpired(session: Session): boolean {
  const expiresAt = Date.parse(session.expiresAt);
  return Number.isNaN(expiresAt) || expiresAt <= Date.now();
}

function readStoredSession(): Session | null {
  try {
    const raw = sessionStorage.getItem(SESSION_KEY);
    if (!raw) return null;
    const session = JSON.parse(raw) as Session;
    if (typeof session?.accessToken !== 'string' || typeof session.user?.id !== 'string') {
      return null;
    }
    return isExpired(session) ? null : session;
  } catch {
    return null;
  }
}

/**
 * Holds the login response (token, expiry and user) in sessionStorage and exposes it as signals.
 * The user comes from the login response; the JWT is never decoded (contract §2.1).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly session = signal<Session | null>(readStoredSession());

  readonly currentUser = computed<User | null>(() => this.session()?.user ?? null);
  readonly role = computed(() => this.currentUser()?.role ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);

  login(credentials: LoginRequest): Observable<User> {
    return this.api.login(credentials).pipe(
      tap((response) => this.store(response)),
      map((response) => response.user),
    );
  }

  logout(): void {
    try {
      sessionStorage.removeItem(SESSION_KEY);
    } catch {
      // Storage unavailable: the in-memory session is still cleared below.
    }
    this.session.set(null);
  }

  /** True when there is a session that hasn't expired. Clears an expired session. */
  hasValidSession(): boolean {
    const session = this.session();
    if (!session) return false;
    if (isExpired(session)) {
      this.logout();
      return false;
    }
    return true;
  }

  /** The bearer token, or null when there is no valid session. */
  token(): string | null {
    return this.hasValidSession() ? this.session()!.accessToken : null;
  }

  private store(response: LoginResponse): void {
    const session: Session = {
      accessToken: response.accessToken,
      expiresAt: response.expiresAt,
      user: response.user,
    };
    try {
      sessionStorage.setItem(SESSION_KEY, JSON.stringify(session));
    } catch {
      // Storage unavailable: keep the session in memory for this tab only.
    }
    this.session.set(session);
  }
}
