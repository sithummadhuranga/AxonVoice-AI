import Link from 'next/link';
import { notFound } from 'next/navigation';
import { AgentEditorForm } from '@/components/agent-editor-form';
import { getAgent } from '@/lib/api';
import { updateAgentAction } from '@/lib/agent-actions';
import {
  createAgentFormState,
  getLanguageLabel,
  getToolLabel,
  type AgentLanguageCode,
  type AgentToolName,
} from '@/lib/agent-form';
import { requireConsoleSession } from '@/lib/console-session';

interface AgentDetailPageProps {
  params: Promise<{ id: string }>;
}

export default async function AgentDetailPage({ params }: AgentDetailPageProps) {
  await requireConsoleSession();
  const { id } = await params;
  const agent = await getAgent(id);
  if (!agent) { notFound(); }

  const formValues = {
    name: agent.name,
    displayName: agent.displayName,
    personaPrompt: agent.personaPrompt,
    primaryLanguage: agent.primaryLanguage as AgentLanguageCode,
    supportedLanguages: (agent.supportedLanguages ?? []) as AgentLanguageCode[],
    voiceName: agent.voiceName ?? '',
    sessionTimeoutSeconds: String(agent.sessionTimeoutSeconds ?? ''),
    silenceTimeoutSeconds: String(agent.silenceTimeoutSeconds ?? ''),
    toolsEnabled: (agent.toolsEnabled ?? []) as AgentToolName[],
    isActive: agent.isActive,
  };

  return (
    <div className="grid gap-6">
      <section className="surface-card-strong relative overflow-hidden p-6 lg:p-8">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{
            background:
              'radial-gradient(circle at 86% 14%, rgba(20,115,230,0.12) 0%, transparent 24%), radial-gradient(circle at 8% 84%, rgba(255,122,69,0.10) 0%, transparent 22%)',
          }}
        />
        <div className="relative flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <p className="eyebrow text-accent">Agent editor</p>
            <h1 className="mt-4 text-[2.4rem] font-semibold tracking-[-0.07em] text-foreground lg:text-[3rem]">
              {agent.displayName}
            </h1>
            <p className="mt-3 max-w-3xl text-sm leading-7 text-muted">
              Adjust the live contract the relay reads at session start, including language scope, timeout policy, and enabled tools.
            </p>
            <div className="mt-5 flex flex-wrap gap-2">
              <Chip>{getLanguageLabel(agent.primaryLanguage)}</Chip>
              <Chip>{agent.voiceName}</Chip>
              <Chip>{agent.geminiModel}</Chip>
            </div>
          </div>
          <span className={`flex items-center gap-1.5 rounded-full px-3 py-1.5 text-[0.68rem] font-semibold uppercase tracking-[0.12em] ${agent.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-black/[0.05] text-muted'}`}>
            <span className={`h-1.5 w-1.5 rounded-full ${agent.isActive ? 'bg-emerald-500' : 'bg-muted/40'}`} />
            {agent.isActive ? 'Active' : 'Paused'}
          </span>
        </div>
      </section>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_20rem]">
        <section className="surface-card-strong p-6 lg:p-7">
          <AgentEditorForm
            action={updateAgentAction}
            hiddenFields={{ agentId: agent.id }}
            initialState={createAgentFormState(formValues)}
            submitLabel="Save changes"
            submitPendingLabel="Saving..."
          />
        </section>

        <aside className="grid gap-4 self-start xl:sticky xl:top-4">
          <section className="surface-card p-5">
            <p className="eyebrow text-muted">Contract summary</p>
            <div className="mt-4 grid gap-3">
              <InfoRow label="Agent ID" value={agent.id} mono />
              <InfoRow label="Primary language" value={getLanguageLabel(agent.primaryLanguage)} />
              <InfoRow label="Supported languages" value={agent.supportedLanguages.map(getLanguageLabel).join(' · ')} />
              <InfoRow label="Gemini model" value={agent.geminiModel} />
            </div>
          </section>

          <section className="surface-card p-5">
            <p className="eyebrow text-muted">Enabled tools</p>
            <div className="mt-4 flex flex-wrap gap-2">
              {agent.toolsEnabled.length > 0 ? (
                agent.toolsEnabled.map((tool) => <Chip key={tool}>{getToolLabel(tool)}</Chip>)
              ) : (
                <p className="text-sm text-muted">No tool contracts are enabled for this agent.</p>
              )}
            </div>
          </section>

          <section className="surface-card p-5">
            <p className="eyebrow text-muted">Update behavior</p>
            <p className="mt-3 text-sm leading-6 text-muted">
              Changes apply to new live sessions immediately. Existing voice sessions keep the configuration that was active when their WebSocket connection was established.
            </p>
          </section>
        </aside>
      </div>
    </div>
  );
}

function InfoRow({ label, mono = false, value }: { label: string; mono?: boolean; value: string }) {
  return (
    <div className="rounded-[1.15rem] border border-line bg-white/78 px-4 py-3">
      <p className="text-xs font-medium uppercase tracking-[0.12em] text-muted">{label}</p>
      <p className={`mt-2 text-sm text-foreground ${mono ? 'break-all font-mono' : 'font-medium'}`}>{value}</p>
    </div>
  );
}

function Chip({ children }: { children: React.ReactNode }) {
  return (
    <span className="rounded-full border border-line bg-white/84 px-3 py-1.5 text-[0.68rem] font-medium uppercase tracking-[0.12em] text-muted">
      {children}
    </span>
  );
}
