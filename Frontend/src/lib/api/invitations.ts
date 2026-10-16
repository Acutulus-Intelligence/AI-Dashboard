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

export function getInvitation(token: string): Promise<InvitationDetails> {
  return apiFetch<InvitationDetails>(`/api/invitations/${encodeURIComponent(token)}`);
}

export function acceptEmailInvitation(
  token: string,
  data: AcceptEmailInvitationRequest,
): Promise<void> {
  return apiFetch<void>(`/api/invitations/${encodeURIComponent(token)}/accept`, {
    method: 'POST',
    body: JSON.stringify(data),
  });
}
