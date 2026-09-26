// TypeScript shapes for the frozen API contract (docs/api-contract.md).
// Organisation IDs never appear here: the contract never sends or returns them.

export type Role = 'SystemAdmin' | 'TenantAdmin' | 'Requester' | 'Approver';

export type RequestStatus = 'Raised' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Completed';

export const REQUEST_STATUSES: readonly RequestStatus[] = [
  'Raised',
  'PendingApproval',
  'Approved',
  'Rejected',
  'Completed',
];

/** The `user` object of the login response (contract §2.2). */
export interface User {
  id: string;
  email: string;
  role: Role;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  /** ISO-8601 UTC timestamp. */
  expiresAt: string;
  user: User;
}

/** `GET /api/sites` item (contract §3.5). */
export interface Site {
  id: string;
  name: string;
}

/** `GET /api/requests` item (contract §3.13). */
export interface RequestSummary {
  id: string;
  siteId: string;
  siteName: string;
  description: string;
  estimatedCost: number;
  status: RequestStatus;
  raisedByUserId: string;
  createdAt: string;
}

/** Single request, and the result of create, approve, reject and complete (contract §3.13). */
export interface RequestDetail {
  id: string;
  siteId: string;
  siteName: string;
  raisedByUserId: string;
  raisedByEmail: string;
  description: string;
  estimatedCost: number;
  actualCost: number | null;
  status: RequestStatus;
  thresholdAtDecision: number | null;
  exceededThreshold: boolean;
  createdAt: string;
  completedAt: string | null;
}

/** `POST /api/requests` body (contract §3.6). */
export interface CreateRequest {
  siteId: string;
  description: string;
  estimatedCost: number;
}

/** `POST /api/requests/{id}/approve` and `/reject` body (contract §3.9, §3.10). */
export interface DecisionRequest {
  comment?: string | null;
}

/** RFC 9457 error body (contract §4). A 401/403 from the middleware may have no body at all. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}
