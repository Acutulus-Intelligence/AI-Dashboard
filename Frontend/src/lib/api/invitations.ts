import { apiFetch } from './client';

export interface InvitationDetails {
  inviteId: string;
  email: string;
  companyName: string;
  roleName: string;
  expiresAt: string;
  hasAccount: boolean;
}

export interface AcceptEmailInvitationRequest {
  firstName: string;
  lastName: string;
  password: string;
}

// The token travels in the request body, never the URL path, so it stays out of
// server logs, browser history, and the Referer header (S3).
export function lookupInvitation(token: string): Promise<InvitationDetails> {
  return apiFetch<InvitationDetails>('/api/invitations/lookup', {
    method: 'POST',
    body: JSON.stringify({ token }),
  });
}

export function acceptEmailInvitation(
  token: string,
  data: AcceptEmailInvitationRequest,
): Promise<void> {
  return apiFetch<void>('/api/invitations/accept', {
    method: 'POST',
    body: JSON.stringify({ token, ...data }),
  });
}
