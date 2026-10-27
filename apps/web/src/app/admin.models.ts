export interface AdminOrganization {
  id: string;
  name: string;
  isActive: boolean;
  version: number;
  userCount: number;
  activeUsers: number;
}
export interface AdminUser {
  id: string;
  email: string;
  displayName: string;
  role: string;
  isActive: boolean;
  invitationPending: boolean;
  invitationExpiresAt: string | null;
  lockoutEnd: string | null;
  version: number;
}
export interface UserDirectory {
  total: number;
  page: number;
  pageSize: number;
  roles: string[];
  users: AdminUser[];
}
export interface SetupLink {
  user: AdminUser;
  setupPath: string;
  expiresAt: string;
}
export interface AdminAudit {
  total: number;
  page: number;
  pageSize: number;
  items: {
    id: number;
    action: string;
    occurredAt: string;
    actor: string;
    detailsJson: string | null;
  }[];
}
