'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { buildApiUrl } from '@/lib/api';
import { requireConsoleSession } from '@/lib/console-session';

export async function updateTenantGeminiApiKey(formData: FormData): Promise<void> {
  const session = await requireConsoleSession();
  const geminiApiKey = readGeminiApiKey(formData);
  const validationError = validateGeminiApiKey(geminiApiKey);

  if (validationError) {
    redirect(buildSetupUrl(validationError));
  }

  const updateError = await submitTenantApiKeyUpdate(session.tenantId, session.accessToken, geminiApiKey);
  if (updateError) {
    redirect(buildSetupUrl(updateError));
  }

  revalidatePath('/dashboard');
  revalidatePath('/setup');
  redirect('/setup?message=' + encodeURIComponent('Gemini API key saved. New live sessions can use it immediately.'));
}

async function submitTenantApiKeyUpdate(
  tenantId: string,
  accessToken: string,
  geminiApiKey: string,
): Promise<string | null> {
  try {
    const response = await fetch(buildApiUrl(`/api/config/tenants/${tenantId}/api-key`), {
      body: JSON.stringify({ geminiApiKey }),
      cache: 'no-store',
      headers: {
        Authorization: `Bearer ${accessToken}`,
        'Content-Type': 'application/json',
      },
      method: 'PUT',
    });

    if (!response.ok) {
      return readErrorMessage(response);
    }

    return null;
  } catch (error) {
    return error instanceof Error
      ? error.message
      : 'The tenant console could not reach the API.';
  }
}

async function readErrorMessage(response: Response): Promise<string> {
  const payload = await response.text();

  if (payload.length === 0) {
    return `The tenant console returned ${response.status}.`;
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

function readGeminiApiKey(formData: FormData): string {
  const value = formData.get('geminiApiKey');
  return typeof value === 'string' ? value.trim() : '';
}

function validateGeminiApiKey(geminiApiKey: string): string | null {
  if (geminiApiKey.length === 0) {
    return 'Gemini API key is required.';
  }

  if (geminiApiKey.length > 512) {
    return 'Gemini API key must be 512 characters or fewer.';
  }

  if (/\s/.test(geminiApiKey)) {
    return 'Gemini API key must not contain spaces or line breaks.';
  }

  return null;
}

function buildSetupUrl(message: string): string {
  const params = new URLSearchParams({ error: message });
  return `/setup?${params.toString()}`;
}