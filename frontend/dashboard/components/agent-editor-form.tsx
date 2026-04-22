'use client';

import { useActionState } from 'react';
import { useFormStatus } from 'react-dom';
import {
  agentLanguageOptions,
  agentToolOptions,
  getLanguageLabel,
  type AgentFormState,
} from '@/lib/agent-form';

type AgentEditorFormProps = {
  action: (state: AgentFormState, formData: FormData) => Promise<AgentFormState>;
  hiddenFields?: Record<string, string>;
  initialState: AgentFormState;
  submitLabel: string;
  submitPendingLabel: string;
};

export function AgentEditorForm({
  action,
  hiddenFields,
  initialState,
  submitLabel,
  submitPendingLabel,
}: AgentEditorFormProps) {
  const [state, formAction] = useActionState(action, initialState);
  const formKey = JSON.stringify(state.values);

  return (
    <form action={formAction} className="grid gap-6" key={formKey}>
      {Object.entries(hiddenFields ?? {}).map(([name, value]) => (
        <input key={name} name={name} type="hidden" value={value} />
      ))}

      {state.error ? (
        <section
          aria-live="polite"
          className="rounded-[1.5rem] border border-dashed border-line px-5 py-4 text-sm leading-6 text-muted"
          role="alert"
        >
          {state.error}
        </section>
      ) : null}

      <div className="grid gap-4 md:grid-cols-2">
        <TextField
          defaultValue={state.values.name}
          label="Internal name"
          name="name"
          placeholder="front-desk"
        />
        <TextField
          defaultValue={state.values.displayName}
          label="Display name"
          name="displayName"
          placeholder="Front Desk Concierge"
        />
      </div>

      <label className="grid gap-2 text-sm text-muted">
        <span>Persona prompt</span>
        <textarea
          className="min-h-40 rounded-[1.25rem] border border-line bg-white/80 px-4 py-3 text-sm leading-6 text-foreground outline-none"
          defaultValue={state.values.personaPrompt}
          name="personaPrompt"
          placeholder="Greet callers warmly, confirm their preferred language, and gather booking details with concise follow-up questions."
          required
          rows={7}
        />
      </label>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <label className="grid gap-2 text-sm text-muted">
          <span>Primary language</span>
          <select
            className="rounded-[1.25rem] border border-line bg-white/80 px-4 py-3 text-sm text-foreground outline-none"
            defaultValue={state.values.primaryLanguage}
            name="primaryLanguage"
          >
            {agentLanguageOptions.map((language) => (
              <option key={language.value} value={language.value}>
                {language.label}
              </option>
            ))}
          </select>
        </label>

        <TextField
          defaultValue={state.values.voiceName}
          label="Voice name"
          name="voiceName"
          placeholder="Aoede"
        />

        <TextField
          defaultValue={state.values.sessionTimeoutSeconds}
          label="Session timeout (seconds)"
          min={1}
          name="sessionTimeoutSeconds"
          type="number"
        />

        <TextField
          defaultValue={state.values.silenceTimeoutSeconds}
          label="Silence timeout (seconds)"
          min={1}
          name="silenceTimeoutSeconds"
          type="number"
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <fieldset className="grid gap-3 rounded-[1.5rem] border border-line bg-white/70 p-5">
          <legend className="px-1 text-sm font-medium text-foreground">Supported languages</legend>
          {agentLanguageOptions.map((language) => (
            <label key={language.value} className="flex items-start gap-3 rounded-[1rem] border border-line/80 bg-white/80 px-4 py-3 text-sm text-foreground">
              <input
                className="mt-1 h-4 w-4 accent-[var(--color-accent)]"
                defaultChecked={state.values.supportedLanguages.includes(language.value)}
                name="supportedLanguages"
                type="checkbox"
                value={language.value}
              />
              <span>
                <span className="block font-medium">{language.label}</span>
                <span className="mt-1 block text-xs leading-5 text-muted">{language.description}</span>
              </span>
            </label>
          ))}
        </fieldset>

        <fieldset className="grid gap-3 rounded-[1.5rem] border border-line bg-white/70 p-5">
          <legend className="px-1 text-sm font-medium text-foreground">Enabled tools</legend>
          {agentToolOptions.map((tool) => (
            <label key={tool.value} className="flex items-start gap-3 rounded-[1rem] border border-line/80 bg-white/80 px-4 py-3 text-sm text-foreground">
              <input
                className="mt-1 h-4 w-4 accent-[var(--color-accent)]"
                defaultChecked={state.values.toolsEnabled.includes(tool.value)}
                name="toolsEnabled"
                type="checkbox"
                value={tool.value}
              />
              <span>
                <span className="block font-medium">{tool.label}</span>
                <span className="mt-1 block text-xs leading-5 text-muted">{tool.description}</span>
              </span>
            </label>
          ))}
        </fieldset>
      </div>

      <label className="flex items-start gap-3 rounded-[1.5rem] border border-line bg-white/70 px-5 py-4 text-sm text-foreground">
        <input
          className="mt-1 h-4 w-4 accent-[var(--color-accent)]"
          defaultChecked={state.values.isActive}
          name="isActive"
          type="checkbox"
        />
        <span>
          <span className="block font-medium">Keep this agent active for the console and relay</span>
          <span className="mt-1 block text-xs leading-5 text-muted">
            Disable the agent if the tenant needs to stop new live sessions without deleting its configuration.
          </span>
        </span>
      </label>

      <div className="flex flex-wrap gap-3 rounded-[1.5rem] border border-line bg-white/70 px-5 py-4 text-xs uppercase tracking-[0.16em] text-muted">
        <span>Primary: {getLanguageLabel(state.values.primaryLanguage)}</span>
        <span>{state.values.supportedLanguages.length} language paths</span>
        <span>{state.values.toolsEnabled.length} tool contracts</span>
      </div>

      <SubmitButton idleLabel={submitLabel} pendingLabel={submitPendingLabel} />
    </form>
  );
}

function SubmitButton({ idleLabel, pendingLabel }: { idleLabel: string; pendingLabel: string }) {
  const { pending } = useFormStatus();

  return (
    <button
      className="rounded-full bg-sidebar px-5 py-3 text-sm font-semibold text-sidebar-foreground transition hover:bg-black disabled:cursor-wait disabled:opacity-80"
      disabled={pending}
      type="submit"
    >
      {pending ? pendingLabel : idleLabel}
    </button>
  );
}

function TextField({
  defaultValue,
  label,
  min,
  name,
  placeholder,
  type = 'text',
}: {
  defaultValue: string;
  label: string;
  min?: number;
  name: string;
  placeholder?: string;
  type?: 'number' | 'text';
}) {
  return (
    <label className="grid gap-2 text-sm text-muted">
      <span>{label}</span>
      <input
        className="rounded-[1.25rem] border border-line bg-white/80 px-4 py-3 text-sm text-foreground outline-none"
        defaultValue={defaultValue}
        min={min}
        name={name}
        placeholder={placeholder}
        required
        type={type}
      />
    </label>
  );
}