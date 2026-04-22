import Link from 'next/link';
import { AgentEditorForm } from '@/components/agent-editor-form';
import { getDashboardOverview } from '@/lib/api';
import { createAgentAction } from '@/lib/agent-actions';
import { createAgentFormState, getLanguageLabel } from '@/lib/agent-form';
import { requireConsoleSession } from '@/lib/console-session';

export default async function AgentsPage() {
  await requireConsoleSession();
  const overview = await getDashboardOverview();

  return (
    <section className="grid gap-6">
      <header className="flex flex-col gap-3 rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)] lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p className="font-mono text-[0.74rem] uppercase tracking-[0.28em] text-accent">
            Agents
          </p>
          <h2 className="mt-4 text-4xl font-semibold tracking-[-0.07em] text-foreground">
            Agent inventory and language coverage
          </h2>
          <p className="mt-3 max-w-2xl text-sm leading-6 text-muted">
            Each agent is scoped to a tenant and controls session behavior, supported languages, and tool access.
          </p>
        </div>
        <div className="rounded-full border border-line bg-white/80 px-4 py-2 text-sm text-foreground">
          {overview.activeAgents} active of {overview.configuredAgents} configured
        </div>
      </header>

      {overview.status === 'unavailable' ? (
        <section className="rounded-[1.75rem] border border-dashed border-line px-6 py-5 text-sm leading-6 text-muted">
          {overview.message}
        </section>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-[1.1fr_0.9fr]">
        <section className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
          <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-accent">
            Create agent
          </p>
          <h3 className="mt-4 text-3xl font-semibold tracking-[-0.06em] text-foreground">
            Define the first live voice contract for this tenant.
          </h3>
          <p className="mt-3 max-w-2xl text-sm leading-6 text-muted">
            This configuration feeds the same authenticated agent contract the relay reads at call start: identity,
            persona, language coverage, tool access, and timeout policy.
          </p>

          <div className="mt-6">
            <AgentEditorForm
              action={createAgentAction}
              initialState={createAgentFormState()}
              submitLabel="Create agent"
              submitPendingLabel="Creating agent..."
            />
          </div>
        </section>

        <section className="grid gap-4 rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
          <div>
            <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-accent">
              Readiness
            </p>
            <h3 className="mt-4 text-2xl font-semibold tracking-[-0.05em] text-foreground">
              Current tenant inventory
            </h3>
          </div>

          <StatusRow
            label="Configured agents"
            value={overview.status === 'ready'
              ? `${overview.configuredAgents} agent${overview.configuredAgents === 1 ? '' : 's'} saved.`
              : 'Inventory is temporarily unavailable.'}
          />
          <StatusRow
            label="Active agents"
            value={overview.status === 'ready'
              ? `${overview.activeAgents} agent${overview.activeAgents === 1 ? '' : 's'} currently ready for live sessions.`
              : 'Active status could not be loaded.'}
          />
          <StatusRow
            label="Primary language coverage"
            value={overview.status === 'ready' && overview.languages.length > 0
              ? overview.languages.map(getLanguageLabel).join(', ')
              : 'No primary-language coverage is configured yet.'}
          />
          <StatusRow
            label="Next milestone"
            value="After the first agent is created, wire business hours and closed dates for live booking safety."
          />
        </section>
      </div>

      <section className="overflow-hidden rounded-4xl border border-line bg-panel shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
        {overview.agents.length === 0 ? (
          <div className="px-6 py-10 text-sm leading-6 text-muted">
            No agents are visible yet. Use the create form above to seed the first tenant-scoped voice contract.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full border-collapse">
              <thead>
                <tr className="border-b border-line text-left text-sm text-muted">
                  <th className="px-6 py-4 font-medium">Agent</th>
                  <th className="px-6 py-4 font-medium">Display name</th>
                  <th className="px-6 py-4 font-medium">Language</th>
                  <th className="px-6 py-4 font-medium">Status</th>
                </tr>
              </thead>
              <tbody>
                {overview.agents.map((agent) => (
                  <tr key={agent.id} className="border-b border-line/70 text-sm text-foreground last:border-b-0">
                    <td className="px-6 py-5 font-semibold tracking-[-0.02em]">
                      <Link href={`/agents/${agent.id}`} className="transition hover:text-accent">
                        {agent.name}
                      </Link>
                    </td>
                    <td className="px-6 py-5">{agent.displayName}</td>
                    <td className="px-6 py-5">{getLanguageLabel(agent.primaryLanguage)}</td>
                    <td className="px-6 py-5">
                      <span className={`rounded-full px-3 py-1 text-xs uppercase tracking-[0.16em] ${agent.isActive ? 'bg-accent-soft text-accent' : 'bg-black/6 text-muted'}`}>
                        {agent.isActive ? 'Active' : 'Disabled'}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </section>
  );
}

function StatusRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-3xl border border-line bg-white/70 px-5 py-4">
      <p className="font-mono text-[0.68rem] uppercase tracking-[0.22em] text-accent">{label}</p>
      <p className="mt-2 text-sm leading-6 text-foreground">{value}</p>
    </div>
  );
}