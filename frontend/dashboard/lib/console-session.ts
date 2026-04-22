import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';

export const consoleSessionCookieName = 'axonvoice_console_session';

type ConsoleJwtPayload = {
  email?: string;
  exp?: number;
  role?: string;
  tenant_id?: string;
  tenant_name?: string;
  token_use?: string;
  user_id?: string;
};

export type ConsoleSession = {
  accessToken: string;
  email: string;
  expiresAt: Date;
  role: string;
  tenantId: string;
  tenantName: string;
  userId: string;
};

export async function getConsoleSession(): Promise<ConsoleSession | null> {
  const cookieStore = await cookies();
  const accessToken = cookieStore.get(consoleSessionCookieName)?.value;

  if (!accessToken) {
    return null;
  }

  const payload = readJwtPayload(accessToken);
  if (!payload || payload.token_use !== 'console' || typeof payload.exp !== 'number') {
    return null;
  }

  if (payload.exp * 1000 <= Date.now()) {
    return null;
  }

  if (
    typeof payload.tenant_id !== 'string'
    || typeof payload.tenant_name !== 'string'
    || typeof payload.user_id !== 'string'
    || typeof payload.email !== 'string'
    || typeof payload.role !== 'string'
  ) {
    return null;
  }

  return {
    accessToken,
    email: payload.email,
    expiresAt: new Date(payload.exp * 1000),
    role: payload.role,
    tenantId: payload.tenant_id,
    tenantName: payload.tenant_name,
    userId: payload.user_id,
  };
}

export async function requireConsoleSession(): Promise<ConsoleSession> {
  const session = await getConsoleSession();

  if (!session) {
    redirect('/login');
  }

  return session;
}

export async function writeConsoleSessionCookie(session: {
  accessToken: string;
  expiresAt: Date | string;
}): Promise<void> {
  const cookieStore = await cookies();
  const expiresAt = new Date(session.expiresAt);

  cookieStore.set(consoleSessionCookieName, session.accessToken, {
    expires: expiresAt,
    httpOnly: true,
    path: '/',
    sameSite: 'lax',
    secure: process.env.NODE_ENV === 'production',
  });
}

export async function clearConsoleSessionCookie(): Promise<void> {
  const cookieStore = await cookies();
  cookieStore.delete(consoleSessionCookieName);
}

function readJwtPayload(token: string): ConsoleJwtPayload | null {
  const tokenParts = token.split('.');
  if (tokenParts.length < 2) {
    return null;
  }

  try {
    const payload = decodeBase64Url(tokenParts[1]);
    const parsed = JSON.parse(payload) as ConsoleJwtPayload;
    return parsed;
  } catch {
    return null;
  }
}

function decodeBase64Url(value: string): string {
  const normalized = value.replace(/-/g, '+').replace(/_/g, '/');
  const padding = normalized.length % 4 === 0
    ? ''
    : '='.repeat(4 - (normalized.length % 4));

  return Buffer.from(`${normalized}${padding}`, 'base64').toString('utf8');
}