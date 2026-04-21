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

export interface DashboardOverview {
  status: 'ready' | 'unavailable';
  message: string | null;
  agents: AgentSummary[];
  configuredAgents: number;
  activeAgents: number;
  languages: string[];
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

export function getErrorMessage(error: unknown): string {
  if (error instanceof ApiClientError) {
    return error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return 'The dashboard could not reach the API.';
}

async function requestJson<T>(path: string, options?: { allowNotFound?: boolean }): Promise<T | null> {
  if (!apiBaseUrl) {
    throw new ApiClientError('Set NEXT_PUBLIC_API_URL before attempting to load dashboard data.');
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    cache: 'no-store',
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
    return 'The dashboard API rejected the request. Tenant authentication still needs to be wired into the console.';
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