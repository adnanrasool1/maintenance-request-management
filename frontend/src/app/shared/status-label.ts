import { RequestStatus } from './api-models';

const LABELS: Record<RequestStatus, string> = {
  Raised: 'Raised',
  PendingApproval: 'Pending approval',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Completed: 'Completed',
};

/** Human-readable label for a status value. */
export function statusLabel(status: RequestStatus): string {
  return LABELS[status] ?? status;
}
