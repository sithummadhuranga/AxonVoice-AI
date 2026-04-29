'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { buildApiUrl } from '@/lib/api';
import {
  agentLanguageOptions,
  agentToolOptions,
  createAgentFormState,
  type AgentFormState,
  type AgentFormValues,
} from '@/lib/agent-form';
import { requireConsoleSession } from '@/lib/console-session';

const supportedLanguageCodes = new Set(agentLanguageOptions.map((option) => option.value));
const supportedToolNames = new Set(agentToolOptions.map((option) => option.value));

type AgentMutationPayload = {
  name: string;
  displayName: string;
  personaPrompt: string;
  primaryLanguage: string;
  supportedLanguages: string[];
  voiceName: string;
  toolsEnabled: string[];
  sessionTimeoutSeconds: number;
  silenceTimeoutSeconds: number;
  isActive: boolean;
};

export async function createAgentAction(
  _previousState: AgentFormState,
  formData: FormData,
): Promise<AgentFormState> {
  const session = await requireConsoleSession();
  const values = readAgentFormValues(formData);
  const validated = validateAgentFormValues(values);

  if (!validated.ok) {
    return createAgentFormState(values, validated.error);
  }

  const result = await submitCreateAgent(session.accessToken, validated.payload);
  if (!result.ok) {
    return createAgentFormState(values, result.error);
  }

  revalidatePath('/agents');
  revalidatePath('/dashboard');
  redirect(`/agents/${result.agentId}?message=${encodeURIComponent('Agent created and ready for new live sessions.')}`);
}

export async function updateAgentAction(
  _previousState: AgentFormState,
  formData: FormData,
): Promise<AgentFormState> {
  const session = await requireConsoleSession();
  const agentId = readTrimmedValue(formData, 'agentId');
  const values = readAgentFormValues(formData);

  if (agentId.length === 0) {
    return createAgentFormState(values, 'Agent id is required.');
  }

  const validated = validateAgentFormValues(values);
  if (!validated.ok) {
    return createAgentFormState(values, validated.error);
  }

  const result = await submitUpdateAgent(agentId, session.accessToken, validated.payload);
  if (!result.ok) {
    return createAgentFormState(values, result.error);
  }

  revalidatePath('/agents');
  revalidatePath(`/agents/${agentId}`);
  revalidatePath('/dashboard');
  redirect(`/agents/${agentId}?message=${encodeURIComponent('Agent settings saved.')}`);
}

async function submitCreateAgent(
  accessToken: string,
  payload: AgentMutationPayload,
): Promise<{ ok: true; agentId: string } | { ok: false; error: string }> {
  try {
    const response = await fetch(buildApiUrl('/api/config/agents'), {
      body: JSON.stringify(payload),
      cache: 'no-store',
      headers: {
        Authorization: `Bearer ${accessToken}`,
        'Content-Type': 'application/json',
      },
      method: 'POST',
    });

    if (!response.ok) {
      return { ok: false, error: await readApiErrorMessage(response) };
    }

    const createdAgent = await response.json() as { id?: string };
    if (typeof createdAgent.id !== 'string' || createdAgent.id.length === 0) {
      return { ok: false, error: 'The agent API did not return the created agent identifier.' };
    }

    return { ok: true, agentId: createdAgent.id };
  } catch (error) {
    return {
      ok: false,
      error: error instanceof Error ? error.message : 'The dashboard could not reach the agent API.',
    };
  }
}

async function submitUpdateAgent(
  agentId: string,
  accessToken: string,
  payload: AgentMutationPayload,
): Promise<{ ok: true } | { ok: false; error: string }> {
  try {
    const response = await fetch(buildApiUrl(`/api/config/agents/${agentId}`), {
      body: JSON.stringify(payload),
      cache: 'no-store',
      headers: {
        Authorization: `Bearer ${accessToken}`,
        'Content-Type': 'application/json',
      },
      method: 'PUT',
    });

    if (!response.ok) {
      return { ok: false, error: await readApiErrorMessage(response) };
    }

    return { ok: true };
  } catch (error) {
    return {
      ok: false,
      error: error instanceof Error ? error.message : 'The dashboard could not reach the agent API.',
    };
  }
}

async function readApiErrorMessage(response: Response): Promise<string> {
  const payload = await response.text();

  if (payload.length === 0) {
    return `The agent API returned ${response.status}.`;
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

function readAgentFormValues(formData: FormData): AgentFormValues {
  return {
    name: readTrimmedValue(formData, 'name'),
    displayName: readTrimmedValue(formData, 'displayName'),
    personaPrompt: readTrimmedValue(formData, 'personaPrompt'),
    primaryLanguage: readTrimmedValue(formData, 'primaryLanguage') as AgentFormValues['primaryLanguage'],
    supportedLanguages: readMultiValue(formData, 'supportedLanguages') as AgentFormValues['supportedLanguages'],
    voiceName: readTrimmedValue(formData, 'voiceName'),
    sessionTimeoutSeconds: readTrimmedValue(formData, 'sessionTimeoutSeconds'),
    silenceTimeoutSeconds: readTrimmedValue(formData, 'silenceTimeoutSeconds'),
    toolsEnabled: readMultiValue(formData, 'toolsEnabled') as AgentFormValues['toolsEnabled'],
    isActive: formData.get('isActive') === 'on',
  };
}

function validateAgentFormValues(
  values: AgentFormValues,
): { ok: true; payload: AgentMutationPayload } | { ok: false; error: string } {
  if (values.name.length === 0) {
    return { ok: false, error: 'Agent name is required.' };
  }

  if (values.name.length > 255) {
    return { ok: false, error: 'Agent name must be 255 characters or fewer.' };
  }

  if (values.displayName.length === 0) {
    return { ok: false, error: 'Display name is required.' };
  }

  if (values.displayName.length > 255) {
    return { ok: false, error: 'Display name must be 255 characters or fewer.' };
  }

  if (values.personaPrompt.length === 0) {
    return { ok: false, error: 'Persona prompt is required.' };
  }

  if (!supportedLanguageCodes.has(values.primaryLanguage)) {
    return { ok: false, error: 'Primary language must be one of: si, ta, en.' };
  }

  if (values.supportedLanguages.length === 0) {
    return { ok: false, error: 'Supported languages must contain at least one language.' };
  }

  if (values.supportedLanguages.some((language) => !supportedLanguageCodes.has(language))) {
    return { ok: false, error: 'Supported languages must be one of: si, ta, en.' };
  }

  if (!values.supportedLanguages.includes(values.primaryLanguage)) {
    return { ok: false, error: 'Supported languages must include the primary language.' };
  }

  if (values.voiceName.length === 0) {
    return { ok: false, error: 'Voice name is required.' };
  }

  if (values.voiceName.length > 100) {
    return { ok: false, error: 'Voice name must be 100 characters or fewer.' };
  }

  if (values.toolsEnabled.some((tool) => !supportedToolNames.has(tool))) {
    return { ok: false, error: 'Tools enabled must contain only supported tool names.' };
  }

  if (values.toolsEnabled.includes('create_pending_booking') && !values.toolsEnabled.includes('check_availability')) {
    return { ok: false, error: 'Booking creation requires availability checking to stay enabled.' };
  }

  const sessionTimeoutSeconds = parsePositiveInteger(values.sessionTimeoutSeconds, 'Session timeout');
  if (!sessionTimeoutSeconds.ok) {
    return sessionTimeoutSeconds;
  }

  const silenceTimeoutSeconds = parsePositiveInteger(values.silenceTimeoutSeconds, 'Silence timeout');
  if (!silenceTimeoutSeconds.ok) {
    return silenceTimeoutSeconds;
  }

  if (silenceTimeoutSeconds.value >= sessionTimeoutSeconds.value) {
    return { ok: false, error: 'Silence timeout must be shorter than the session timeout.' };
  }

  return {
    ok: true,
    payload: {
      name: values.name,
      displayName: values.displayName,
      personaPrompt: values.personaPrompt,
      primaryLanguage: values.primaryLanguage,
      supportedLanguages: values.supportedLanguages,
      voiceName: values.voiceName,
      toolsEnabled: values.toolsEnabled,
      sessionTimeoutSeconds: sessionTimeoutSeconds.value,
      silenceTimeoutSeconds: silenceTimeoutSeconds.value,
      isActive: values.isActive,
    },
  };
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

function readTrimmedValue(formData: FormData, fieldName: string): string {
  const value = formData.get(fieldName);
  return typeof value === 'string' ? value.trim() : '';
}

function readMultiValue(formData: FormData, fieldName: string): string[] {
  return formData
    .getAll(fieldName)
    .filter((value): value is string => typeof value === 'string')
    .map((value) => value.trim())
    .filter((value) => value.length > 0);
}