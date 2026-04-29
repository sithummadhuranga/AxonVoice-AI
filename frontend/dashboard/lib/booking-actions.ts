'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { buildApiUrl } from '@/lib/api';
import { requireConsoleSession } from '@/lib/console-session';

const weekdayIndexes = [0, 1, 2, 3, 4, 5, 6] as const;

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

export async function setBusinessHoursAction(formData: FormData): Promise<void> {
  const session = await requireConsoleSession();
  const agentId = readRequiredValue(formData, 'agentId');
  const scheduleResult = readBusinessHoursSchedule(formData);

  if (!scheduleResult.ok) {
    redirect(buildBookingsUrl(agentId, 'error', scheduleResult.error));
  }

  const error = await submitMutation({
    accessToken: session.accessToken,
    path: `/api/config/agents/${agentId}/business-hours`,
    method: 'PUT',
    body: JSON.stringify({ schedule: scheduleResult.value }),
    contentType: 'application/json',
  });

  if (error) {
    redirect(buildBookingsUrl(agentId, 'error', error));
  }

  revalidatePath('/bookings');

  const message = scheduleResult.value.length === 0
    ? 'Saved an empty schedule. Reservation and appointment tools stay inactive until at least one day is enabled.'
    : 'Business hours saved. Availability checks can now use the updated weekly schedule.';

  redirect(buildBookingsUrl(agentId, 'message', message));
}

async function submitMutation(options: {
  accessToken: string;
  path: string;
  method: 'POST' | 'PUT';
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

function readBusinessHoursSchedule(
  formData: FormData,
): { ok: true; value: BusinessHoursSlotPayload[] } | { ok: false; error: string } {
  const schedule: BusinessHoursSlotPayload[] = [];

  for (const dayOfWeek of weekdayIndexes) {
    if (formData.get(`dayEnabled_${dayOfWeek}`) !== 'on') {
      continue;
    }

    const openTime = readRequiredValue(formData, `openTime_${dayOfWeek}`);
    const closeTime = readRequiredValue(formData, `closeTime_${dayOfWeek}`);
    const slotDuration = parsePositiveInteger(readRequiredValue(formData, `slotDurationMinutes_${dayOfWeek}`), 'Slot duration');
    if (!slotDuration.ok) {
      return slotDuration;
    }

    const capacity = parsePositiveInteger(readRequiredValue(formData, `maxCapacityPerSlot_${dayOfWeek}`), 'Slot capacity');
    if (!capacity.ok) {
      return capacity;
    }

    if (openTime >= closeTime) {
      return { ok: false, error: `${weekdayLabels[dayOfWeek]} must close after it opens.` };
    }

    schedule.push({
      dayOfWeek,
      openTime: normalizeTimeValue(openTime),
      closeTime: normalizeTimeValue(closeTime),
      slotDurationMinutes: slotDuration.value,
      maxCapacityPerSlot: capacity.value,
    });
  }

  return { ok: true, value: schedule };
}

function parsePositiveInteger(
  value: string,
  fieldLabel: string,
): { ok: true; value: number } | { ok: false; error: string } {
  const parsed = Number.parseInt(value, 10);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return { ok: false, error: `${fieldLabel} must be greater than zero.` };
  }

  return { ok: true, value: parsed };
}

function normalizeTimeValue(value: string): string {
  return value.length === 5 ? `${value}:00` : value;
}

type BusinessHoursSlotPayload = {
  dayOfWeek: number;
  openTime: string;
  closeTime: string;
  slotDurationMinutes: number;
  maxCapacityPerSlot: number;
};

const weekdayLabels = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

function buildBookingsUrl(agentId: string, key: 'error' | 'message', value: string): string {
  const params = new URLSearchParams({ agentId, [key]: value });
  return `/bookings?${params.toString()}`;
}