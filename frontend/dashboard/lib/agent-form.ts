export type AgentLanguageCode = 'si' | 'ta' | 'en';
export type AgentToolName = 'check_availability' | 'create_pending_booking';

export type AgentFormValues = {
  name: string;
  displayName: string;
  personaPrompt: string;
  primaryLanguage: AgentLanguageCode;
  supportedLanguages: AgentLanguageCode[];
  voiceName: string;
  sessionTimeoutSeconds: string;
  silenceTimeoutSeconds: string;
  toolsEnabled: AgentToolName[];
  isActive: boolean;
};

export type AgentFormState = {
  error: string | null;
  values: AgentFormValues;
};

export const agentLanguageOptions: ReadonlyArray<{
  value: AgentLanguageCode;
  label: string;
  description: string;
}> = [
  { value: 'si', label: 'Sinhala', description: 'Primary voice experience for Sinhala-speaking callers.' },
  { value: 'ta', label: 'Tamil', description: 'Serve Tamil-speaking callers with the same workflow.' },
  { value: 'en', label: 'English', description: 'Keep an English path available for mixed-language traffic.' },
];

export const agentToolOptions: ReadonlyArray<{
  value: AgentToolName;
  label: string;
  description: string;
}> = [
  {
    value: 'check_availability',
    label: 'Check availability',
    description: 'Let the agent inspect capacity before it promises a slot.',
  },
  {
    value: 'create_pending_booking',
    label: 'Create pending booking',
    description: 'Hold a slot after the caller confirms the reservation details.',
  },
];

const defaultAgentFormValues: AgentFormValues = {
  name: '',
  displayName: '',
  personaPrompt: '',
  primaryLanguage: 'si',
  supportedLanguages: ['si', 'ta', 'en'],
  voiceName: 'Aoede',
  sessionTimeoutSeconds: '600',
  silenceTimeoutSeconds: '90',
  toolsEnabled: ['check_availability', 'create_pending_booking'],
  isActive: true,
};

export function createAgentFormValues(overrides?: Partial<AgentFormValues>): AgentFormValues {
  return {
    ...defaultAgentFormValues,
    ...overrides,
    supportedLanguages: overrides?.supportedLanguages ? [...overrides.supportedLanguages] : [...defaultAgentFormValues.supportedLanguages],
    toolsEnabled: overrides?.toolsEnabled ? [...overrides.toolsEnabled] : [...defaultAgentFormValues.toolsEnabled],
  };
}

export function createAgentFormState(overrides?: Partial<AgentFormValues>, error: string | null = null): AgentFormState {
  return {
    error,
    values: createAgentFormValues(overrides),
  };
}

export function getLanguageLabel(language: string): string {
  switch (language) {
    case 'si':
      return 'Sinhala';
    case 'ta':
      return 'Tamil';
    case 'en':
      return 'English';
    default:
      return language;
  }
}

export function getToolLabel(tool: string): string {
  switch (tool) {
    case 'check_availability':
      return 'Check availability';
    case 'create_pending_booking':
      return 'Create pending booking';
    default:
      return tool.replaceAll('_', ' ');
  }
}