import Link from 'next/link';
import { AgentEditorForm } from '@/components/agent-editor-form';
import { createAgentAction } from '@/lib/agent-actions';
import { createAgentFormState } from '@/lib/agent-form';
import { requireConsoleSession } from '@/lib/console-session';

export default async function NewAgentPage() {
  await requireConsoleSession();
  const defaults = createAgentFormState().values;

  return (
    <div className="grid gap-6">
      <section className="surface-card-strong relative overflow-hidden p-6 lg:p-8">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{
            background:
              'radial-gradient(circle at 14% 20%, rgba(20,115,230,0.12) 0%, transparent 28%), radial-gradient(circle at 92% 80%, rgba(255,122,69,0.10) 0%, transparent 24%)',
          }}
        />
        <div className="relative max-w-3xl">
          <p className="eyebrow text-accent">New voice contract</p>
          <h1 className="page-title mt-4 text-foreground">Ship the next agent with a cleaner setup flow.</h1>
          <p className="page-subtitle mt-5">
            Define identity, language coverage, and tool access in one pass. The resulting contract feeds the same runtime path used when a live session starts.
          </p>
        </div>
      </section>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_20rem]">
        <section className="surface-card-strong p-6 lg:p-7">
          <AgentEditorForm
            action={createAgentAction}
            initialState={createAgentFormState()}
            submitLabel="Create agent"
            submitPendingLabel="Creating agent..."
          />
        </section>

        <aside className="grid gap-4 self-start xl:sticky xl:top-4">
          <section className="surface-card p-5">
            <p className="eyebrow text-muted">Configuration guide</p>
            <h2 className="mt-2 text-lg font-semibold tracking-[-0.03em] text-foreground">
              What to lock down first
            </h2>
            <ul className="mt-4 grid gap-3">
              {[
                { label: 'Internal name', detail: 'A short slug used in API paths. Use lowercase with hyphens.' },
                { label: 'Display name', detail: 'What callers hear when the agent introduces itself.' },
                { label: 'Persona prompt', detail: 'Sets tone, behaviour, and task focus for the AI voice.' },
                { label: 'Primary language', detail: 'The default language for new calls before caller preference is detected.' },
                { label: 'Tools', detail: 'Enable booking and availability tools once business hours are configured.' },
              ].map((tip) => (
                <li key={tip.label} className="rounded-[1.15rem] border border-line bg-white/78 px-4 py-3 text-xs">
                  <p className="font-medium text-foreground">{tip.label}</p>
                  <p className="mt-1 leading-5 text-muted">{tip.detail}</p>
                </li>
              ))}
            </ul>
          </section>

          <section className="surface-card p-5">
            <p className="eyebrow text-muted">Default runtime</p>
            <div className="mt-4 grid gap-3">
              <MetricBlock label="Primary language" value={defaults.primaryLanguage.toUpperCase()} />
              <MetricBlock label="Session timeout" value={`${defaults.sessionTimeoutSeconds}s`} />
              <MetricBlock label="Silence timeout" value={`${defaults.silenceTimeoutSeconds}s`} />
            </div>
          </section>
        </aside>
      </div>
    </div>
  );
}

function MetricBlock({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[1.15rem] border border-line bg-white/78 px-4 py-3">
      <p className="text-xs font-medium uppercase tracking-[0.12em] text-muted">{label}</p>
      <p className="mt-2 text-sm font-semibold text-foreground">{value}</p>
    </div>
  );
}
