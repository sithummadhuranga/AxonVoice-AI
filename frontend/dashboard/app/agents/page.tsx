import Link from 'next/link';
import { getDashboardOverview } from '@/lib/api';

export default async function AgentsPage() {
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

      <section className="overflow-hidden rounded-4xl border border-line bg-panel shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
        {overview.agents.length === 0 ? (
          <div className="px-6 py-10 text-sm leading-6 text-muted">
            No agents are visible yet. Once authenticated gateway access is available, this table will hydrate from
            <span className="font-mono"> /api/config/agents</span>.
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
                    <td className="px-6 py-5">{languageLabel(agent.primaryLanguage)}</td>
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