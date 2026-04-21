import Link from 'next/link';
import { getDashboardOverview } from '@/lib/api';

export default async function DashboardPage() {
  const overview = await getDashboardOverview();

  return (
    <section className="grid gap-8">
      <div className="grid gap-6 lg:grid-cols-[1.25fr_0.75fr]">
        <section className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
          <p className="font-mono text-[0.74rem] uppercase tracking-[0.28em] text-accent">
            Overview
          </p>
          <h2 className="mt-4 max-w-3xl text-5xl font-semibold tracking-[-0.08em] text-foreground">
            Bring each tenant from configuration to live voice traffic.
          </h2>
          <p className="mt-4 max-w-2xl text-base leading-7 text-muted">
            This console focuses on agent readiness first: languages, personas, tools, and session boundaries.
            Live analytics can layer in after the authenticated tenant flow is connected.
          </p>
        </section>

        <section className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
          <p className="font-mono text-[0.74rem] uppercase tracking-[0.28em] text-accent">
            Data status
          </p>
          <p className="mt-4 text-2xl font-semibold tracking-[-0.05em] text-foreground">
            {overview.status === 'ready' ? 'Live configuration connected' : 'Awaiting dashboard API access'}
          </p>
          <p className="mt-3 text-sm leading-6 text-muted">
            {overview.message ?? 'The dashboard can read agent configuration data from the gateway route.'}
          </p>
        </section>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <MetricCard label="Configured agents" value={overview.configuredAgents.toString()} />
        <MetricCard label="Active agents" value={overview.activeAgents.toString()} />
        <MetricCard
          label="Primary languages"
          value={overview.languages.length > 0 ? overview.languages.join(', ') : 'No data'}
        />
      </div>

      <div className="grid gap-6 lg:grid-cols-[1.2fr_0.8fr]">
        <section className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
          <div className="flex items-center justify-between gap-4">
            <div>
              <p className="font-mono text-[0.74rem] uppercase tracking-[0.28em] text-accent">
                Agent roster
              </p>
              <h3 className="mt-3 text-2xl font-semibold tracking-[-0.05em] text-foreground">
                Latest configured agents
              </h3>
            </div>
            <Link
              href="/agents"
              className="rounded-full border border-line bg-white/80 px-4 py-2 text-sm text-foreground transition hover:bg-white"
            >
              View all
            </Link>
          </div>

          <div className="mt-6 grid gap-3">
            {overview.agents.length === 0 ? (
              <EmptyPanel message={overview.message ?? 'Agents will appear here once the dashboard can query the gateway.'} />
            ) : (
              overview.agents.slice(0, 4).map((agent) => (
                <Link
                  key={agent.id}
                  href={`/agents/${agent.id}`}
                  className="rounded-3xl border border-line bg-white/70 px-5 py-4 transition hover:bg-white"
                >
                  <div className="flex items-center justify-between gap-4">
                    <div>
                      <p className="text-lg font-semibold tracking-[-0.03em] text-foreground">
                        {agent.displayName}
                      </p>
                      <p className="mt-1 text-sm text-muted">{agent.name}</p>
                    </div>
                    <span className="rounded-full bg-accent-soft px-3 py-1 text-xs uppercase tracking-[0.16em] text-accent">
                      {languageLabel(agent.primaryLanguage)}
                    </span>
                  </div>
                </Link>
              ))
            )}
          </div>
        </section>

        <section className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
          <p className="font-mono text-[0.74rem] uppercase tracking-[0.28em] text-accent">
            Launch checklist
          </p>
          <div className="mt-6 grid gap-3">
            {[
              'Verify NEXT_PUBLIC_API_URL points to the gateway.',
              'Wire dashboard authentication so tenant-scoped agent routes return live data.',
              'Configure business hours and document ingestion before enabling public traffic.',
            ].map((item) => (
              <div key={item} className="rounded-3xl border border-line bg-white/70 px-5 py-4 text-sm leading-6 text-foreground">
                {item}
              </div>
            ))}
          </div>
        </section>
      </div>
    </section>
  );
}

function MetricCard({ label, value }: { label: string; value: string }) {
  return (
    <section className="rounded-[1.75rem] border border-line bg-panel px-6 py-5 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
      <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-muted">{label}</p>
      <p className="mt-4 text-3xl font-semibold tracking-[-0.06em] text-foreground">{value}</p>
    </section>
  );
}

function EmptyPanel({ message }: { message: string }) {
  return (
    <div className="rounded-3xl border border-dashed border-line px-5 py-8 text-sm leading-6 text-muted">
      {message}
    </div>
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