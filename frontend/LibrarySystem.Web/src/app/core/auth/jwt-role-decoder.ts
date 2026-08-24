import { JWT_ROLE_CLAIM_KEYS } from './auth-roles';

type JwtPayload = Record<string, unknown>;

const JWT_USERNAME_CLAIM_KEYS = [
  'unique_name',
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'
] as const;

export function decodeJwtRoles(accessToken: string | null): string[] {
  if (!accessToken) {
    return [];
  }

  const payload = decodeJwtPayload(accessToken);

  if (!payload) {
    return [];
  }

  const roles = new Set<string>();

  for (const claimKey of JWT_ROLE_CLAIM_KEYS) {
    addRoleClaimValue(roles, payload[claimKey]);
  }

  return [...roles];
}

export function decodeJwtUsername(accessToken: string | null): string | null {
  if (!accessToken) {
    return null;
  }

  const payload = decodeJwtPayload(accessToken);

  if (!payload) {
    return null;
  }

  for (const claimKey of JWT_USERNAME_CLAIM_KEYS) {
    const username = getStringClaimValue(payload[claimKey]);

    if (username) {
      return username;
    }
  }

  return null;
}

function decodeJwtPayload(accessToken: string): JwtPayload | null {
  const [, payloadSegment] = accessToken.split('.');

  if (!payloadSegment) {
    return null;
  }

  try {
    const json = decodeBase64Url(payloadSegment);
    const payload = JSON.parse(json) as unknown;

    return isJwtPayload(payload) ? payload : null;
  } catch {
    return null;
  }
}

function decodeBase64Url(value: string): string {
  const base64 = value.replaceAll('-', '+').replaceAll('_', '/');
  const paddedBase64 = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
  const binary = atob(paddedBase64);
  const bytes = Uint8Array.from(binary, (character) => character.charCodeAt(0));

  return new TextDecoder().decode(bytes);
}

function isJwtPayload(value: unknown): value is JwtPayload {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function getStringClaimValue(value: unknown): string | null {
  return typeof value === 'string' && value.trim() ? value : null;
}

function addRoleClaimValue(roles: Set<string>, value: unknown): void {
  if (typeof value === 'string' && value.trim()) {
    roles.add(value);
    return;
  }

  if (!Array.isArray(value)) {
    return;
  }

  for (const role of value) {
    if (typeof role === 'string' && role.trim()) {
      roles.add(role);
    }
  }
}
