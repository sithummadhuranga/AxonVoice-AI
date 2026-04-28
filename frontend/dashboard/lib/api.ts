import 'server-only';

import { getConsoleSession, type ConsoleSession } from '@/lib/console-session';

export interface AgentSummary {
  id: string;
  name: string;
  displayName: string;
  primaryLanguage: string;
  isActive: boolean;
}

export interface AgentDetail {
  id: string;
  tenantId: string;
  name: string;
  displayName: string;
  personaPrompt: string;
  supportedLanguages: string[];
  primaryLanguage: string;
  voiceName: string;
  geminiModel: string;
  sessionTimeoutSeconds: number;
  silenceTimeoutSeconds: number;
  toolsEnabled: string[];
  isActive: boolean;
}

export interface TenantDetail {
  id: string;
  name: string;
  apiKeyHint: string | null;
  defaultLanguage: string;
  rateLimitDaily: number;
  rateLimitConcurrent: number;
  isActive: boolean;
}

export interface DashboardOverview {
  status: 'ready' | 'unavailable';
  message: string | null;
  agents: AgentSummary[];
  configuredAgents: number;
  activeAgents: number;
  languages: string[];
}

export interface SessionSummary {
  id: string;
  agentId: string;
  language: string;
  status: string;
  startedAt: string;
  durationSeconds: number | null;
}

export interface SessionFunctionCall {
  id: string;
  sessionId: string;
  tenantId: string;
  functionName: string;
  argumentsJson: string | null;
  resultJson: string | null;
  succeeded: boolean;
  errorMessage: string | null;
  calledAt: string;
  durationMs: number;
}

export interface SessionDetail {
  id: string;
  agentId: string;
  tenantId: string;
  callerIdentifier: string;
  language: string;
  status: string;
  summary: string | null;
  startedAt: string;
  endedAt: string | null;
  durationSeconds: number | null;
  functionCalls: SessionFunctionCall[];
}

export interface SessionListResult {
  total: number;
  page: number;
  pageSize: number;
  sessions: SessionSummary[];
}

export interface PendingBookingSummary {
  id: string;
  customerName: string;
  customerPhone: string;
  customerLanguage: string;
  partySize: number;
  requestedDatetime: string;
  specialRequests: string | null;
  status: string;
  expiresAt: string;
  createdAt: string;
  confirmationCode: string;
}

export interface ConfirmedBookingSummary {
  id: string;
  promotedFromPendingId: string | null;
  customerName: string;
  customerPhone: string;
  customerLanguage: string;
  partySize: number;
  bookingDatetime: string;
  specialRequests: string | null;
  status: string;
  internalNotes: string | null;
  confirmedAt: string;
  confirmedBy: string | null;
  updatedAt: string;
  confirmationCode: string;
}

export interface AgentBookings {
  pendingBookings: PendingBookingSummary[];
  confirmedBookings: ConfirmedBookingSummary[];
}

export interface BusinessHoursEntry {
  id: string;
  dayOfWeek: number;
  openTime: string;
  closeTime: string;
  slotDurationMinutes: number;
  maxCapacityPerSlot: number;
  isActive: boolean;
}

export interface ClosedDateEntry {
  id: string;
  date: string;
  reason: string | null;
}

export type KnowledgeDocumentStatus = 'uploading' | 'processing' | 'ready' | 'error';

export interface KnowledgeDocumentSummary {
  id: string;
  filename: string;
  status: KnowledgeDocumentStatus;
  chunkCount: number | null;
  uploadedAt: string;
}

class ApiClientError extends Error {
  readonly status?: number;

  constructor(message: string, status?: number) {
    super(message);
    this.name = 'ApiClientError';
    this.status = status;
  }
}

const apiBaseUrl = (process.env.AXONVOICE_API_URL ?? process.env.NEXT_PUBLIC_API_URL ?? '').replace(/\/$/, '');

export function buildApiUrl(path: string): string {
  return `${getApiBaseUrl()}${path}`;
}

export async function getDashboardOverview(): Promise<DashboardOverview> {
  try {
    const agents = await getAgents();
    const languages = Array.from(new Set(agents.map((agent) => agent.primaryLanguage)));

    return {
      status: 'ready',
      message: null,
      agents,
      configuredAgents: agents.length,
      activeAgents: agents.filter((agent) => agent.isActive).length,
      languages,
    };
  } catch (error) {
    return {
      status: 'unavailable',
      message: getErrorMessage(error),
      agents: [],
      configuredAgents: 0,
      activeAgents: 0,
      languages: [],
    };
  }
}

export async function getAgents(): Promise<AgentSummary[]> {
  const result = await requestJson<AgentSummary[]>('/api/config/agents');
  return result ?? [];
}

export async function getAgent(id: string): Promise<AgentDetail | null> {
  return requestJson<AgentDetail>(`/api/config/agents/${id}`, { allowNotFound: true });
}

export async function getCurrentTenant(): Promise<TenantDetail> {
  const session = await getRequiredConsoleSession();
  const tenant = await requestJson<TenantDetail>(`/api/config/tenants/${session.tenantId}`, { session });

  if (!tenant) {
    throw new ApiClientError('The tenant configuration could not be loaded.');
  }

  return tenant;
}

export async function getSession(id: string): Promise<SessionDetail | null> {
  return requestJson<SessionDetail>(`/api/conversations/sessions/${id}`, { allowNotFound: true });
}

export async function getSessions(options?: {
  agentId?: string;
  page?: number;
  pageSize?: number;
}): Promise<SessionListResult> {
  const params = new URLSearchParams();

  if (options?.agentId) {
    params.set('agentId', options.agentId);
  }

  if (typeof options?.page === 'number') {
    params.set('page', String(options.page));
  }

  if (typeof options?.pageSize === 'number') {
    params.set('pageSize', String(options.pageSize));
  }

  const query = params.size > 0 ? `?${params.toString()}` : '';
  const result = await requestJson<SessionListResult>(`/api/conversations/sessions${query}`);

  if (!result) {
    throw new ApiClientError('The sessions API did not return a payload.');
  }

  return result;
}

export async function getAgentBookings(agentId: string): Promise<AgentBookings> {
  const result = await requestJson<AgentBookings>(`/api/config/agents/${agentId}/bookings`);

  if (!result) {
    throw new ApiClientError('The bookings API did not return a payload.');
  }

  return result;
}

export async function getBusinessHours(agentId: string): Promise<BusinessHoursEntry[]> {
  const result = await requestJson<BusinessHoursEntry[]>(`/api/config/agents/${agentId}/business-hours`);
  return result ?? [];
}

export async function getClosedDates(agentId: string): Promise<ClosedDateEntry[]> {
  const result = await requestJson<ClosedDateEntry[]>(`/api/config/agents/${agentId}/business-hours/closed-dates`);
  return result ?? [];
}

export async function getKnowledgeDocuments(agentId: string): Promise<KnowledgeDocumentSummary[]> {
  const result = await requestJson<KnowledgeDocumentSummary[]>(`/api/knowledge/agents/${agentId}/documents`);
  return result ?? [];
}

export function hasGeminiApiKeyConfigured(tenant: Pick<TenantDetail, 'apiKeyHint'>): boolean {
  return typeof tenant.apiKeyHint === 'string' && tenant.apiKeyHint.trim().length > 0;
}

export function getErrorMessage(error: unknown): string {
  if (error instanceof ApiClientError) {
    return error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return 'The dashboard could not reach the API.';
}

async function requestJson<T>(
  path: string,
  options?: { allowNotFound?: boolean; session?: ConsoleSession },
): Promise<T | null> {
  const session = options?.session ?? await getRequiredConsoleSession();

  const response = await fetch(buildApiUrl(path), {
    cache: 'no-store',
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
    },
  });

  if (options?.allowNotFound && response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new ApiClientError(await buildErrorMessage(response), response.status);
  }

  return response.json() as Promise<T>;
}

async function buildErrorMessage(response: Response): Promise<string> {
  if (response.status === 401 || response.status === 403) {
    return 'Your tenant console session is no longer valid. Sign in again to continue.';
  }

  if (response.status === 404) {
    return 'The requested dashboard resource was not found.';
  }

  const payload = await response.text();
  if (payload.trim().length > 0) {
    return `The dashboard API returned ${response.status}: ${payload}`;
  }

  return `The dashboard API returned ${response.status}.`;
}

function getApiBaseUrl(): string {
  if (!apiBaseUrl) {
    throw new ApiClientError('Set AXONVOICE_API_URL or NEXT_PUBLIC_API_URL before attempting to load dashboard data.');
  }

  return apiBaseUrl;
}

async function getRequiredConsoleSession(): Promise<ConsoleSession> {
  const session = await getConsoleSession();
  if (!session) {
    throw new ApiClientError('Sign in to access the tenant console.');
  }

  return session;
}