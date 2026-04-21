import Link from 'next/link';
import { notFound } from 'next/navigation';
import { getAgent, getErrorMessage } from '@/lib/api';

type AgentDetailPageProps = {
  params: Promise<{ id: string }>;
};

export default async function AgentDetailPage({ params }: AgentDetailPageProps) {
  const { id } = await params;

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
              This view uses the live agent detail contract. Mutation wiring can be added on top once tenant auth is
              available in the dashboard.
            </p>
          </div>
          {agent ? (
            <span className={`rounded-full px-4 py-2 text-sm ${agent.isActive ? 'bg-accent-soft text-accent' : 'bg-black/6 text-muted'}`}>
              {agent.isActive ? 'Active' : 'Disabled'}
            </span>
          ) : null}
        </div>
      </header>

      {loadError ? (
        <section className="rounded-[1.75rem] border border-dashed border-line px-6 py-5 text-sm leading-6 text-muted">
          {loadError}
        </section>
      ) : null}

      {agent ? (
        <div className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
          <section className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
            <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-accent">Identity</p>
            <div className="mt-6 grid gap-5">
              <ReadonlyField label="Internal name" value={agent.name} />
              <ReadonlyField label="Display name" value={agent.displayName} />
              <ReadonlyField label="Primary language" value={languageLabel(agent.primaryLanguage)} />
              <ReadonlyField label="Voice" value={agent.voiceName} />
              <ReadonlyField label="Gemini model" value={agent.geminiModel} />
            </div>

            <label className="mt-6 grid gap-2 text-sm text-muted">
              <span>Persona prompt</span>
              <textarea
                readOnly
                value={agent.personaPrompt}
                rows={8}
                className="rounded-[1.25rem] border border-line bg-white/80 px-4 py-3 text-sm leading-6 text-foreground outline-none"
              />
            </label>
          </section>

          <div className="grid gap-6">
            <section className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
              <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-accent">Session policy</p>
              <div className="mt-6 grid gap-5">
                <ReadonlyField label="Session timeout" value={`${agent.sessionTimeoutSeconds} seconds`} />
                <ReadonlyField label="Silence timeout" value={`${agent.silenceTimeoutSeconds} seconds`} />
                <ReadonlyField label="Supported languages" value={agent.supportedLanguages.map(languageLabel).join(', ')} />
              </div>
            </section>

            <section className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
              <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-accent">Enabled tools</p>
              <div className="mt-5 flex flex-wrap gap-2">
                {agent.toolsEnabled.map((tool) => (
                  <span key={tool} className="rounded-full bg-accent-soft px-3 py-2 text-xs uppercase tracking-[0.16em] text-accent">
                    {tool.replaceAll('_', ' ')}
                  </span>
                ))}
              </div>
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

function languageLabel(language: string): string {
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

async function loadAgentDetail(id: string): Promise<{ agent: Awaited<ReturnType<typeof getAgent>> | null; loadError: string | null }> {
  try {
    const agent = await getAgent(id);
    return { agent, loadError: null };
  } catch (error) {
    return { agent: null, loadError: getErrorMessage(error) };
  }
}