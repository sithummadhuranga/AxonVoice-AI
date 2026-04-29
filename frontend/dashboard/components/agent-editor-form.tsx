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
  const fieldClassName =
    'w-full rounded-[1rem] border border-line-strong bg-white/86 px-4 py-3 text-sm text-foreground shadow-[inset_0_1px_0_rgba(255,255,255,0.82)] outline-none transition focus:border-accent/40 focus:ring-4 focus:ring-accent-soft';

  return (
    <form action={formAction} className="grid gap-5" key={formKey}>
      {Object.entries(hiddenFields ?? {}).map(([name, value]) => (
        <input key={name} name={name} type="hidden" value={value} />
      ))}

      {state.error ? (
        <section
          aria-live="polite"
          className="rounded-[1.35rem] border border-red-200/80 bg-red-50/88 px-5 py-4 text-sm leading-6 text-red-700"
          role="alert"
        >
          {state.error}
        </section>
      ) : null}

      <SectionFrame
        description="Define how the agent is named internally and how it introduces itself to callers."
        title="Identity"
      >
        <div className="grid gap-4 md:grid-cols-2">
          <TextField
            className={fieldClassName}
            defaultValue={state.values.name}
            label="Internal name"
            name="name"
            placeholder="front-desk"
          />
          <TextField
            className={fieldClassName}
            defaultValue={state.values.displayName}
            label="Display name"
            name="displayName"
            placeholder="Front Desk Concierge"
          />
        </div>
      </SectionFrame>

      <SectionFrame
        description="Set the speaking tone, conversation style, and operating boundaries the model should follow."
        title="Conversation profile"
      >
        <label className="grid gap-2 text-sm text-muted">
          <span className="font-medium text-foreground">Persona prompt</span>
          <textarea
            className={`${fieldClassName} min-h-44 resize-y leading-7`}
            defaultValue={state.values.personaPrompt}
            name="personaPrompt"
            placeholder="Greet callers warmly, answer business questions clearly, and only use the enabled tools after confirming the needed details."
            required
            rows={7}
          />
        </label>

        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <label className="grid gap-2 text-sm text-muted">
            <span className="font-medium text-foreground">Primary language</span>
            <select
              className={fieldClassName}
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
            className={fieldClassName}
            defaultValue={state.values.voiceName}
            label="Voice name"
            name="voiceName"
            placeholder="Sulafat"
          />

          <TextField
            className={fieldClassName}
            defaultValue={state.values.sessionTimeoutSeconds}
            label="Session timeout (seconds)"
            min={1}
            name="sessionTimeoutSeconds"
            type="number"
          />

          <TextField
            className={fieldClassName}
            defaultValue={state.values.silenceTimeoutSeconds}
            label="Silence timeout (seconds)"
            min={1}
            name="silenceTimeoutSeconds"
            type="number"
          />
        </div>
      </SectionFrame>

      <div className="grid gap-4 lg:grid-cols-2">
        <SectionFrame description="Tell the relay which spoken paths this agent is allowed to handle." title="Supported languages">
          {agentLanguageOptions.map((language) => (
            <label key={language.value} className="flex items-start gap-3 rounded-[1.15rem] border border-line bg-white/78 px-4 py-3.5 text-sm text-foreground transition hover:border-accent/20 hover:bg-white/92">
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
        </SectionFrame>

        <SectionFrame description="Only enable the workflows this agent truly owns. Booking tools require business hours; ordering works best once pricing or catalog knowledge is loaded." title="Enabled tools">
          {agentToolOptions.map((tool) => (
            <label key={tool.value} className="flex items-start gap-3 rounded-[1.15rem] border border-line bg-white/78 px-4 py-3.5 text-sm text-foreground transition hover:border-accent/20 hover:bg-white/92">
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
        </SectionFrame>
      </div>

      <label className="flex items-start gap-3 rounded-[1.5rem] border border-line bg-white/76 px-5 py-4 text-sm text-foreground shadow-[var(--shadow-md)]">
        <input
          className="mt-1 h-4 w-4 accent-[var(--color-accent)]"
          defaultChecked={state.values.isActive}
          name="isActive"
          type="checkbox"
        />
        <span>
          <span className="block font-medium">Keep this agent active for new live sessions</span>
          <span className="mt-1 block text-xs leading-6 text-muted">
            Turn this off when the tenant needs to pause fresh traffic without deleting the agent definition.
          </span>
        </span>
      </label>

      <div className="flex flex-col gap-4 rounded-[1.5rem] border border-line bg-white/74 px-5 py-4 shadow-[var(--shadow-md)] md:flex-row md:items-center md:justify-between">
        <div className="flex flex-wrap gap-2">
          <SummaryChip label={`Primary ${getLanguageLabel(state.values.primaryLanguage)}`} />
          <SummaryChip label={`${state.values.supportedLanguages.length} language paths`} />
          <SummaryChip label={`${state.values.toolsEnabled.length} enabled tools`} />
        </div>
        <SubmitButton idleLabel={submitLabel} pendingLabel={submitPendingLabel} />
      </div>
    </form>
  );
}

function SubmitButton({ idleLabel, pendingLabel }: { idleLabel: string; pendingLabel: string }) {
  const { pending } = useFormStatus();

  return (
    <button
      className="primary-button w-full disabled:cursor-wait disabled:opacity-80 md:w-auto"
      disabled={pending}
      type="submit"
    >
      {pending ? pendingLabel : idleLabel}
    </button>
  );
}

function SectionFrame({
  children,
  description,
  title,
}: {
  children: React.ReactNode;
  description: string;
  title: string;
}) {
  return (
    <section className="surface-card grid gap-4 p-5 sm:p-6">
      <div>
        <h3 className="text-sm font-semibold tracking-[-0.02em] text-foreground">{title}</h3>
        <p className="mt-1 text-sm leading-6 text-muted">{description}</p>
      </div>
      {children}
    </section>
  );
}

function SummaryChip({ label }: { label: string }) {
  return (
    <span className="rounded-full border border-line bg-white/82 px-3 py-1.5 text-xs font-medium uppercase tracking-[0.12em] text-muted">
      {label}
    </span>
  );
}

function TextField({
  className,
  defaultValue,
  label,
  min,
  name,
  placeholder,
  type = 'text',
}: {
  className: string;
  defaultValue: string;
  label: string;
  min?: number;
  name: string;
  placeholder?: string;
  type?: 'number' | 'text';
}) {
  return (
    <label className="grid gap-2 text-sm text-muted">
      <span className="font-medium text-foreground">{label}</span>
      <input
        className={className}
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