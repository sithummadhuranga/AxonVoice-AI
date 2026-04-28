'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { buildApiUrl } from '@/lib/api';
import { requireConsoleSession } from '@/lib/console-session';

export async function confirmPendingBookingAction(formData: FormData): Promise<void> {
  const session = await requireConsoleSession();
  const agentId = readRequiredValue(formData, 'agentId');
  const bookingId = readRequiredValue(formData, 'bookingId');
  const internalNotes = readOptionalValue(formData, 'internalNotes');

  const error = await submitMutation({
    accessToken: session.accessToken,
    path: `/api/config/agents/${agentId}/bookings/pending/${bookingId}/confirm`,
    method: 'POST',
    body: JSON.stringify({ internalNotes }),
    contentType: 'application/json',
  });

  if (error) {
    redirect(buildBookingsUrl(agentId, 'error', error));
  }

  revalidatePath('/bookings');
  redirect(buildBookingsUrl(agentId, 'message', 'Pending booking confirmed.'));
}

export async function expirePendingBookingAction(formData: FormData): Promise<void> {
  const session = await requireConsoleSession();
  const agentId = readRequiredValue(formData, 'agentId');
  const bookingId = readRequiredValue(formData, 'bookingId');

  const error = await submitMutation({
    accessToken: session.accessToken,
    path: `/api/config/agents/${agentId}/bookings/pending/${bookingId}/expire`,
    method: 'POST',
  });

  if (error) {
    redirect(buildBookingsUrl(agentId, 'error', error));
  }

  revalidatePath('/bookings');
  redirect(buildBookingsUrl(agentId, 'message', 'Pending booking expired.'));
}

async function submitMutation(options: {
  accessToken: string;
  path: string;
  method: 'POST';
  body?: BodyInit;
  contentType?: string;
}): Promise<string | null> {
  try {
    const headers = new Headers({
      Authorization: `Bearer ${options.accessToken}`,
    });

    if (options.contentType) {
      headers.set('Content-Type', options.contentType);
    }

    const response = await fetch(buildApiUrl(options.path), {
      body: options.body,
      cache: 'no-store',
      headers,
      method: options.method,
    });

    if (!response.ok) {
      return readErrorMessage(response);
    }

    return null;
  } catch (error) {
    return error instanceof Error ? error.message : 'The bookings workflow could not reach the API.';
  }
}

async function readErrorMessage(response: Response): Promise<string> {
  const payload = await response.text();

  if (payload.length === 0) {
    return `The bookings API returned ${response.status}.`;
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

function readRequiredValue(formData: FormData, fieldName: string): string {
  const value = readOptionalValue(formData, fieldName);
  if (!value) {
    throw new Error(`${fieldName} is required.`);
  }

  return value;
}

function readOptionalValue(formData: FormData, fieldName: string): string | null {
  const value = formData.get(fieldName);
  if (typeof value !== 'string') {
    return null;
  }

  const trimmedValue = value.trim();
  return trimmedValue.length > 0 ? trimmedValue : null;
}

function buildBookingsUrl(agentId: string, key: 'error' | 'message', value: string): string {
  const params = new URLSearchParams({ agentId, [key]: value });
  return `/bookings?${params.toString()}`;
}