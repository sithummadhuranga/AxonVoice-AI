import Link from 'next/link';
import { getDashboardOverview } from '@/lib/api';
import { getLanguageLabel } from '@/lib/agent-form';
import { requireConsoleSession } from '@/lib/console-session';

export default async function AgentsPage() {
  await requireConsoleSession();
  const overview = await getDashboardOverview();
  const activeShare =
    overview.configuredAgents > 0
      ? `${Math.round((overview.activeAgents / overview.configuredAgents) * 100)}% of agents are live-ready`
      : 'No live-ready agents yet';

  return (
    <div className="grid gap-6">
      <section className="surface-card-strong relative overflow-hidden p-6 lg:p-8">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{
            background:
              'radial-gradient(circle at 84% 14%, rgba(20,115,230,0.12) 0%, transparent 24%), radial-gradient(circle at 12% 80%, rgba(255,122,69,0.10) 0%, transparent 22%)',
          }}
        />
        <div className="relative grid gap-6 xl:grid-cols-[minmax(0,1fr)_18rem] xl:items-start">
          <div>
            <p className="eyebrow text-accent">Agent Inventory</p>
            <h1 className="page-title mt-4 max-w-3xl text-foreground">
              Every live voice contract in one operational inventory.
            </h1>
            <p className="page-subtitle mt-5">
              Review which agents can take traffic now, check language coverage, and move into edit flows without a noisy table-first experience.
            </p>
            <div className="mt-7 flex flex-wrap gap-3">
              <Link href="/agents/new" className="primary-button">
                Create agent
              </Link>
              <Link href="/dashboard" className="secondary-button">
                Return to overview
              </Link>
            </div>
          </div>

          <div className="grid gap-3">
            <SummaryCard label="Configured agents" value={overview.configuredAgents.toString()} />
            <SummaryCard label="Active posture" value={activeShare} />
            <SummaryCard
              label="Language coverage"
              value={overview.languages.map(getLanguageLabel).join(' · ') || 'Not configured'}
            />
          </div>
        </div>
      </section>

      {overview.status === 'unavailable' ? (
        <div className="flex items-start gap-3 rounded-[1.35rem] border border-amber-200/80 bg-amber-50/90 px-4 py-3.5 text-sm text-amber-700">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none" className="mt-0.5 shrink-0" aria-hidden="true">
            <path d="M8 1.5L14.5 13H1.5L8 1.5Z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round"/>
            <path d="M8 6v3.5M8 11v.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
          </svg>
          {overview.message}
        </div>
      ) : null}

      <section className="surface-card-strong overflow-hidden p-5 lg:p-6">
        <div className="flex items-center justify-between gap-4 border-b border-line pb-4">
          <div>
            <p className="eyebrow text-muted">Current list</p>
            <h2 className="mt-2 text-lg font-semibold tracking-[-0.03em] text-foreground">
              Workspace agents
            </h2>
          </div>
          {overview.agents.length > 0 ? (
            <span className="rounded-full border border-line bg-white/80 px-3 py-1.5 text-xs font-medium uppercase tracking-[0.12em] text-muted">
              {overview.agents.length} visible
            </span>
          ) : null}
        </div>

        {overview.agents.length === 0 ? (
          <div className="flex flex-col items-center gap-4 px-6 py-16 text-center">
            <div className="flex h-14 w-14 items-center justify-center rounded-[1.4rem] bg-accent-soft text-accent">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <circle cx="12" cy="8" r="4" stroke="currentColor" strokeWidth="1.5"/>
                <path d="M4 20c0-4.418 3.582-8 8-8s8 3.582 8 8" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
                <path d="M16 3l2 2M19 7h-2M16 11l2-2" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round"/>
              </svg>
            </div>
            <div>
              <p className="text-base font-semibold tracking-[-0.02em] text-foreground">No agents configured</p>
              <p className="mt-2 text-sm leading-6 text-muted">
                Create the first voice agent to define language coverage, persona, and tool access for this tenant.
              </p>
            </div>
            <Link href="/agents/new" className="primary-button">
              Create first agent
            </Link>
          </div>
        ) : (
          <ul className="mt-4 grid gap-3">
            {overview.agents.map((agent) => (
              <li key={agent.id}>
                <Link
                  href={`/agents/${agent.id}`}
                  className="group flex flex-col gap-4 rounded-[1.5rem] border border-line bg-white/76 px-5 py-4 transition hover:-translate-y-0.5 hover:border-accent/18 hover:bg-white/92 md:flex-row md:items-center"
                >
                  <div className="flex min-w-0 flex-1 items-center gap-4">
                    <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-[1.2rem] bg-accent-soft text-sm font-semibold text-accent">
                      {agent.displayName.charAt(0).toUpperCase()}
                    </div>
                    <div className="min-w-0">
                      <p className="truncate text-base font-semibold tracking-[-0.02em] text-foreground">
                        {agent.displayName}
                      </p>
                      <p className="mt-1 truncate text-sm text-muted">{agent.name}</p>
                    </div>
                  </div>

                  <div className="flex flex-wrap items-center gap-2 md:justify-end">
                    <span className="rounded-full border border-line bg-white/84 px-3 py-1.5 text-[0.68rem] font-medium uppercase tracking-[0.12em] text-muted">
                      {getLanguageLabel(agent.primaryLanguage)}
                    </span>
                    <span
                      className={`rounded-full px-3 py-1.5 text-[0.68rem] font-medium uppercase tracking-[0.12em] ${
                        agent.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-black/[0.05] text-muted'
                      }`}
                    >
                      {agent.isActive ? 'Active' : 'Paused'}
                    </span>
                  </div>

                  <span className="flex items-center gap-2 text-sm font-medium text-accent transition group-hover:gap-3">
                    Edit
                    <svg width="14" height="14" viewBox="0 0 14 14" fill="none" aria-hidden="true">
                      <path d="M5 3L9 7L5 11" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                    </svg>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

function SummaryCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[1.45rem] border border-line bg-white/82 px-4 py-4 shadow-[var(--shadow-md)]">
      <p className="text-xs font-medium uppercase tracking-[0.12em] text-muted">{label}</p>
      <p className="mt-2 text-sm leading-6 text-foreground">{value}</p>
    </div>
  );
}
