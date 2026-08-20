export const AUTH_ROLES = {
  Admin: 'Admin',
  Member: 'Member'
} as const;

export const JWT_ROLE_CLAIM_KEYS = [
  'role',
  'roles',
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
] as const;
