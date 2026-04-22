import Link from 'next/link';
import { notFound } from 'next/navigation';
import { AgentEditorForm } from '@/components/agent-editor-form';
import { getAgent, getErrorMessage } from '@/lib/api';
import { updateAgentAction } from '@/lib/agent-actions';
import { createAgentFormState, getLanguageLabel, getToolLabel } from '@/lib/agent-form';
import { requireConsoleSession } from '@/lib/console-session';

type AgentDetailPageProps = {
  params: Promise<{ id: string }>;
  searchParams: Promise<{
    created?: string;
    updated?: string;
  }>;
};

export default async function AgentDetailPage({ params, searchParams }: AgentDetailPageProps) {
  await requireConsoleSession();
  const { id } = await params;
  const { created, updated } = await searchParams;

  const { agent, loadError } = await loadAgentDetail(id);

  if (!agent && !loadError) {
    notFound();
  }

  return (
    <section className="grid gap-6">
      <header className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
        <Link href="/agents" className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-accent">
          Back to agents
        </Link>
        <div className="mt-4 flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <h2 className="text-4xl font-semibold tracking-[-0.07em] text-foreground">
              {agent?.displayName ?? 'Agent unavailable'}
            </h2>
            <p className="mt-3 max-w-2xl text-sm leading-6 text-muted">
              Adjust the tenant-scoped live agent contract here without bypassing the API path the relay already trusts.
            </p>
          </div>
          {agent ? (
            <span className={`rounded-full px-4 py-2 text-sm ${agent.isActive ? 'bg-accent-soft text-accent' : 'bg-black/6 text-muted'}`}>
              {agent.isActive ? 'Active' : 'Disabled'}
            </span>
          ) : null}
        </div>
      </header>

      {created ? (
        <MessagePanel message="Agent created successfully. Finish the runtime contract here before wiring business hours." />
      ) : null}

      {updated ? (
        <MessagePanel message="Agent settings updated successfully." />
      ) : null}

      {loadError ? (
        <section className="rounded-[1.75rem] border border-dashed border-line px-6 py-5 text-sm leading-6 text-muted">
          {loadError}
        </section>
      ) : null}

      {agent ? (
        <div className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
          <section className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
            <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-accent">Edit contract</p>
            <h3 className="mt-4 text-3xl font-semibold tracking-[-0.06em] text-foreground">
              Identity, language coverage, and tool access
            </h3>
            <p className="mt-3 max-w-2xl text-sm leading-6 text-muted">
              Changes here revalidate the tenant console pages and persist through the same authenticated API surface the
              session relay reads from at runtime.
            </p>

            <div className="mt-6">
              <AgentEditorForm
                action={updateAgentAction}
                hiddenFields={{ agentId: agent.id }}
                initialState={createAgentFormState({
                  name: agent.name,
                  displayName: agent.displayName,
                  personaPrompt: agent.personaPrompt,
                  primaryLanguage: agent.primaryLanguage as 'si' | 'ta' | 'en',
                  supportedLanguages: agent.supportedLanguages as Array<'si' | 'ta' | 'en'>,
                  voiceName: agent.voiceName,
                  sessionTimeoutSeconds: String(agent.sessionTimeoutSeconds),
                  silenceTimeoutSeconds: String(agent.silenceTimeoutSeconds),
                  toolsEnabled: agent.toolsEnabled as Array<'check_availability' | 'create_pending_booking'>,
                  isActive: agent.isActive,
                })}
                submitLabel="Save agent settings"
                submitPendingLabel="Saving agent settings..."
              />
            </div>
          </section>

          <div className="grid gap-6">
            <section className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
              <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-accent">Runtime contract</p>
              <div className="mt-6 grid gap-5">
                <ReadonlyField label="Agent id" value={agent.id} />
                <ReadonlyField label="Tenant id" value={agent.tenantId} />
                <ReadonlyField label="Gemini model" value={agent.geminiModel} />
                <ReadonlyField label="Primary language" value={getLanguageLabel(agent.primaryLanguage)} />
                <ReadonlyField label="Supported languages" value={agent.supportedLanguages.map(getLanguageLabel).join(', ')} />
              </div>
            </section>

            <section className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
              <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-accent">Live tool preview</p>
              <div className="mt-5 flex flex-wrap gap-2">
                {agent.toolsEnabled.map((tool) => (
                  <span key={tool} className="rounded-full bg-accent-soft px-3 py-2 text-xs uppercase tracking-[0.16em] text-accent">
                    {getToolLabel(tool)}
                  </span>
                ))}
              </div>
              <p className="mt-4 text-sm leading-6 text-muted">
                The booking hold path remains guarded so pending booking creation cannot stay enabled after availability
                checks are disabled.
              </p>
            </section>
          </div>
        </div>
      ) : null}
    </section>
  );
}

function ReadonlyField({ label, value }: { label: string; value: string }) {
  return (
    <label className="grid gap-2 text-sm text-muted">
      <span>{label}</span>
      <input
        readOnly
        value={value}
        className="rounded-[1.25rem] border border-line bg-white/80 px-4 py-3 text-sm text-foreground outline-none"
      />
    </label>
  );
}

function MessagePanel({ message }: { message: string }) {
  return (
    <section className="rounded-[1.75rem] border border-dashed border-line px-6 py-5 text-sm leading-6 text-muted">
      {message}
    </section>
  );
}

async function loadAgentDetail(id: string): Promise<{ agent: Awaited<ReturnType<typeof getAgent>> | null; loadError: string | null }> {
  try {
    const agent = await getAgent(id);
    return { agent, loadError: null };
  } catch (error) {
    return { agent: null, loadError: getErrorMessage(error) };
  }
}