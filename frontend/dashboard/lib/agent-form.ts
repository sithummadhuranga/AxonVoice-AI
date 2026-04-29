export type AgentLanguageCode = 'si' | 'ta' | 'en';
export type AgentToolName = 'check_availability' | 'create_pending_booking' | 'place_order';

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
    description: 'Let the agent inspect capacity before it promises a table, appointment, seat, or time slot.',
  },
  {
    value: 'create_pending_booking',
    label: 'Create pending booking',
    description: 'Hold a confirmed reservation or appointment until staff reviews it.',
  },
  {
    value: 'place_order',
    label: 'Place order',
    description: 'Store a confirmed order after the caller agrees to the final item list and total.',
  },
];

const defaultAgentFormValues: AgentFormValues = {
  name: '',
  displayName: '',
  personaPrompt: '',
  primaryLanguage: 'si',
  supportedLanguages: ['si', 'ta', 'en'],
  voiceName: 'Sulafat',
  sessionTimeoutSeconds: '600',
  silenceTimeoutSeconds: '90',
  toolsEnabled: [],
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
    case 'place_order':
      return 'Place order';
    default:
      return tool.replaceAll('_', ' ');
  }
}