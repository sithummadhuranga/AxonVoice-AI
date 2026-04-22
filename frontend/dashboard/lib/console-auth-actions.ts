'use server';

import { redirect } from 'next/navigation';
import { buildApiUrl } from '@/lib/api';
import { clearConsoleSessionCookie, writeConsoleSessionCookie } from '@/lib/console-session';

type ConsoleAuthEnvelope = {
  accessToken: string;
  expiresAt: string;
};

export async function loginConsoleUser(formData: FormData): Promise<void> {
  await submitConsoleAuthRequest('/api/config/auth/login', {
    email: readFormValue(formData, 'email'),
    password: readFormValue(formData, 'password'),
  }, 'login', '/dashboard');
}

export async function registerConsoleOwner(formData: FormData): Promise<void> {
  await submitConsoleAuthRequest('/api/config/auth/register', {
    businessName: readFormValue(formData, 'businessName'),
    email: readFormValue(formData, 'email'),
    password: readFormValue(formData, 'password'),
  }, 'register', '/setup');
}

export async function logoutConsoleUser(): Promise<void> {
  await clearConsoleSessionCookie();
  redirect('/login');
}

async function submitConsoleAuthRequest(
  path: string,
  payload: Record<string, string>,
  mode: 'login' | 'register',
  successPath: '/dashboard' | '/setup',
): Promise<void> {
  const result = await requestConsoleAuth(path, payload);
  if ('errorMessage' in result) {
    redirect(buildLoginUrl(mode, result.errorMessage));
  }

  await writeConsoleSessionCookie(result.auth);
  redirect(successPath);
}

async function requestConsoleAuth(
  path: string,
  payload: Record<string, string>,
): Promise<{ auth: ConsoleAuthEnvelope } | { errorMessage: string }> {
  try {
    const response = await fetch(buildApiUrl(path), {
      body: JSON.stringify(payload),
      cache: 'no-store',
      headers: {
        'Content-Type': 'application/json',
      },
      method: 'POST',
    });

    if (!response.ok) {
      return { errorMessage: await readErrorMessage(response) };
    }

    return {
      auth: await response.json() as ConsoleAuthEnvelope,
    };
  } catch (error) {
    return {
      errorMessage: error instanceof Error
        ? error.message
        : 'The tenant console could not reach the API.',
    };
  }
}

async function readErrorMessage(response: Response): Promise<string> {
  const payload = await response.text();

  if (payload.length === 0) {
    return response.status === 401
      ? 'Email or password is incorrect.'
      : `The tenant console returned ${response.status}.`;
  }

  try {
    const parsed = JSON.parse(payload) as { error?: string };
    if (typeof parsed.error === 'string' && parsed.error.length > 0) {
      return parsed.error;
    }
  } catch {
    return payload;
  }

  return payload;
}

function readFormValue(formData: FormData, fieldName: string): string {
  const value = formData.get(fieldName);

  return typeof value === 'string' ? value.trim() : '';
}

function buildLoginUrl(mode: 'login' | 'register', message: string): string {
  const params = new URLSearchParams({
    error: message,
    mode,
  });

  return `/login?${params.toString()}`;
}