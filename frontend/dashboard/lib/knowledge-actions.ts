'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { buildApiUrl } from '@/lib/api';
import { requireConsoleSession } from '@/lib/console-session';

export async function uploadKnowledgeDocumentAction(formData: FormData): Promise<void> {
  const session = await requireConsoleSession();
  const agentId = readRequiredValue(formData, 'agentId');
  const file = formData.get('file');
  const language = readOptionalValue(formData, 'language');

  if (!(file instanceof File) || file.size === 0) {
    redirect(buildKnowledgeBaseUrl(agentId, 'error', 'Choose a document before uploading.'));
  }

  const uploadBody = new FormData();
  uploadBody.set('file', file);

  const query = language ? `?language=${encodeURIComponent(language)}` : '';
  const error = await submitKnowledgeRequest({
    accessToken: session.accessToken,
    path: `/api/knowledge/agents/${agentId}/documents/upload${query}`,
    method: 'POST',
    body: uploadBody,
  });

  if (error) {
    redirect(buildKnowledgeBaseUrl(agentId, 'error', error));
  }

  revalidatePath('/knowledge-base');
  redirect(buildKnowledgeBaseUrl(agentId, 'message', 'Document upload accepted. Refresh shortly to see ingestion progress.'));
}

export async function deleteKnowledgeDocumentAction(formData: FormData): Promise<void> {
  const session = await requireConsoleSession();
  const agentId = readRequiredValue(formData, 'agentId');
  const documentId = readRequiredValue(formData, 'documentId');

  const error = await submitKnowledgeRequest({
    accessToken: session.accessToken,
    path: `/api/knowledge/agents/${agentId}/documents/${documentId}`,
    method: 'DELETE',
  });

  if (error) {
    redirect(buildKnowledgeBaseUrl(agentId, 'error', error));
  }

  revalidatePath('/knowledge-base');
  redirect(buildKnowledgeBaseUrl(agentId, 'message', 'Document deleted.'));
}

async function submitKnowledgeRequest(options: {
  accessToken: string;
  path: string;
  method: 'POST' | 'DELETE';
  body?: BodyInit;
}): Promise<string | null> {
  try {
    const response = await fetch(buildApiUrl(options.path), {
      body: options.body,
      cache: 'no-store',
      headers: {
        Authorization: `Bearer ${options.accessToken}`,
      },
      method: options.method,
    });

    if (!response.ok) {
      return readErrorMessage(response);
    }

    return null;
  } catch (error) {
    return error instanceof Error ? error.message : 'The knowledge base workflow could not reach the API.';
  }
}

async function readErrorMessage(response: Response): Promise<string> {
  const payload = await response.text();

  if (payload.length === 0) {
    return `The knowledge base API returned ${response.status}.`;
  }

  try {
    const parsed = JSON.parse(payload) as { Message?: string; error?: string; message?: string };
    if (typeof parsed.error === 'string' && parsed.error.length > 0) {
      return parsed.error;
    }

    if (typeof parsed.message === 'string' && parsed.message.length > 0) {
      return parsed.message;
    }

    if (typeof parsed.Message === 'string' && parsed.Message.length > 0) {
      return parsed.Message;
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

function buildKnowledgeBaseUrl(agentId: string, key: 'error' | 'message', value: string): string {
  const params = new URLSearchParams({ agentId, [key]: value });
  return `/knowledge-base?${params.toString()}`;
}