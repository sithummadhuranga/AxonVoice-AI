import Link from 'next/link';
import {
  getAgents,
  getErrorMessage,
  getSessions,
  type AgentSummary,
  type SessionSummary,
} from '@/lib/api';
import { getLanguageLabel } from '@/lib/agent-form';
import { formatConsoleDateTime, formatDurationSeconds } from '@/lib/console-format';
import { requireConsoleSession } from '@/lib/console-session';

type SessionsPageProps = {
  searchParams: Promise<{
    agentId?: string;
    page?: string;
  }>;
};

const pageSize = 20;

export default async function SessionsPage({ searchParams }: SessionsPageProps) {
  await requireConsoleSession();

  const [{ agentId: requestedAgentId, page: requestedPage }, agents] = await Promise.all([
    searchParams,
    getAgents().catch(() => []),
  ]);

  const selectedAgentId = agents.some((agent) => agent.id === requestedAgentId) ? requestedAgentId : undefined;
  const selectedAgent = agents.find((agent) => agent.id === selectedAgentId) ?? null;
  const page = parsePositiveInteger(requestedPage) ?? 1;

  let sessions: SessionSummary[] = [];
  let totalSessions = 0;
  let loadError: string | null = null;

  try {
    const result = await getSessions({
      agentId: selectedAgentId,
      page,
      pageSize,
    });

    sessions = result.sessions;
    totalSessions = result.total;
  } catch (error) {
    loadError = getErrorMessage(error);
  }

  const activeSessions = sessions.filter((session) => session.status === 'active').length;
  const closedSessions = sessions.filter((session) => session.status === 'closed').length;
  const totalPages = Math.max(1, Math.ceil(totalSessions / pageSize));

  return (
    <div className="grid gap-6">
      <section className="surface-card-strong relative overflow-hidden p-6 lg:p-8">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{
            background:
              'radial-gradient(circle at 88% 14%, rgba(20,115,230,0.12) 0%, transparent 24%), radial-gradient(circle at 10% 86%, rgba(255,122,69,0.10) 0%, transparent 22%)',
          }}
        />
        <div className="relative grid gap-6 xl:grid-cols-[minmax(0,1fr)_18rem] xl:items-start">
          <div>
            <p className="eyebrow text-accent">Sessions</p>
            <h1 className="page-title mt-4 max-w-3xl text-foreground">
              Inspect live and completed voice traffic without leaving the operator console.
            </h1>
            <p className="page-subtitle mt-5">
              Review session health, jump into tool-call traces, and isolate one agent&apos;s traffic when you need to understand a booking flow or a production incident.
            </p>
            <div className="mt-7 flex flex-wrap gap-3">
              <Link href="/dashboard" className="secondary-button">
                Return to overview
              </Link>
              <Link href={buildSessionsUrl(selectedAgentId, 1)} className="secondary-button">
                Refresh list
              </Link>
            </div>
          </div>

          <div className="grid gap-3">
            <SummaryCard label="Visible sessions" value={totalSessions.toString()} />
            <SummaryCard label="Active in page" value={activeSessions.toString()} />
            <SummaryCard label="Current filter" value={selectedAgent?.displayName ?? 'All agents'} />
          </div>
        </div>
      </section>

      {loadError ? (
        <div className="flex items-start gap-3 rounded-[1.35rem] border border-amber-200/80 bg-amber-50/90 px-4 py-3.5 text-sm text-amber-700">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none" className="mt-0.5 shrink-0" aria-hidden="true">
            <path d="M8 1.5L14.5 13H1.5L8 1.5Z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round"/>
            <path d="M8 6v3.5M8 11v.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
          </svg>
          {loadError}
        </div>
      ) : null}

      <section className="surface-card-strong overflow-hidden p-5 lg:p-6">
        <div className="flex flex-col gap-4 border-b border-line pb-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <p className="eyebrow text-muted">Agent filter</p>
            <h2 className="mt-2 text-lg font-semibold tracking-[-0.03em] text-foreground">
              Session traffic scope
            </h2>
          </div>
          <div className="flex flex-wrap gap-2">
            <FilterChip href={buildSessionsUrl(undefined, 1)} active={!selectedAgentId}>
              All agents
            </FilterChip>
            {agents.map((agent) => (
              <FilterChip key={agent.id} href={buildSessionsUrl(agent.id, 1)} active={agent.id === selectedAgentId}>
                {agent.displayName}
              </FilterChip>
            ))}
          </div>
        </div>

        {sessions.length === 0 ? (
          <div className="flex flex-col items-center gap-4 px-6 py-16 text-center">
            <div className="flex h-14 w-14 items-center justify-center rounded-[1.4rem] bg-accent-soft text-accent">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M4 17l4-5 3 3 5-8 4 5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
              </svg>
            </div>
            <div>
              <p className="text-base font-semibold tracking-[-0.02em] text-foreground">No sessions visible yet</p>
              <p className="mt-2 text-sm leading-6 text-muted">
                {selectedAgent
                  ? `${selectedAgent.displayName} has not produced any recorded sessions for this filter yet.`
                  : 'Session telemetry will appear here once the relay starts recording live calls.'}
              </p>
            </div>
          </div>
        ) : (
          <>
            <div className="mt-4 grid gap-3">
              {sessions.map((session) => (
                <SessionCard
                  key={session.id}
                  agent={agents.find((candidate) => candidate.id === session.agentId) ?? null}
                  session={session}
                />
              ))}
            </div>

            {totalPages > 1 ? (
              <div className="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-line pt-4 text-sm text-muted">
                <span>
                  Page {page} of {totalPages} · {closedSessions} closed in this slice
                </span>
                <div className="flex gap-2">
                  <Link
                    aria-disabled={page <= 1}
                    href={page <= 1 ? buildSessionsUrl(selectedAgentId, 1) : buildSessionsUrl(selectedAgentId, page - 1)}
                    className={`secondary-button ${page <= 1 ? 'pointer-events-none opacity-50' : ''}`}
                  >
                    Previous
                  </Link>
                  <Link
                    aria-disabled={page >= totalPages}
                    href={page >= totalPages ? buildSessionsUrl(selectedAgentId, totalPages) : buildSessionsUrl(selectedAgentId, page + 1)}
                    className={`secondary-button ${page >= totalPages ? 'pointer-events-none opacity-50' : ''}`}
                  >
                    Next
                  </Link>
                </div>
              </div>
            ) : null}
          </>
        )}
      </section>
    </div>
  );
}

function SessionCard({ agent, session }: { agent: AgentSummary | null; session: SessionSummary }) {
  return (
    <Link
      href={`/sessions/${session.id}`}
      className="group flex flex-col gap-4 rounded-[1.5rem] border border-line bg-white/76 px-5 py-4 transition hover:-translate-y-0.5 hover:border-accent/18 hover:bg-white/92"
    >
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-base font-semibold tracking-[-0.02em] text-foreground">
              {agent?.displayName ?? 'Unknown agent'}
            </span>
            <StatusChip status={session.status} />
          </div>
          <p className="mt-2 text-sm text-muted">Started {formatConsoleDateTime(session.startedAt)}</p>
        </div>

        <div className="flex flex-wrap gap-2 lg:justify-end">
          <MetricChip label="Language" value={getLanguageLabel(session.language)} />
          <MetricChip label="Duration" value={formatDurationSeconds(session.durationSeconds)} />
        </div>
      </div>

      <div className="flex items-center justify-between gap-3">
        <span className="truncate text-sm text-muted">{session.id}</span>
        <span className="flex items-center gap-2 text-sm font-medium text-accent transition group-hover:gap-3">
          Inspect
          <svg width="14" height="14" viewBox="0 0 14 14" fill="none" aria-hidden="true">
            <path d="M5 3L9 7L5 11" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
          </svg>
        </span>
      </div>
    </Link>
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

function FilterChip({ active, children, href }: { active: boolean; children: React.ReactNode; href: string }) {
  return (
    <Link
      href={href}
      className={`rounded-full border px-3 py-1.5 text-xs font-medium uppercase tracking-[0.12em] transition ${
        active
          ? 'border-accent/16 bg-accent-soft text-accent'
          : 'border-line bg-white/84 text-muted hover:border-accent/16 hover:text-foreground'
      }`}
    >
      {children}
    </Link>
  );
}

function MetricChip({ label, value }: { label: string; value: string }) {
  return (
    <span className="rounded-full border border-line bg-white/84 px-3 py-1.5 text-[0.68rem] font-medium uppercase tracking-[0.12em] text-muted">
      {label}: {value}
    </span>
  );
}

function StatusChip({ status }: { status: string }) {
  const className = status === 'active'
    ? 'bg-emerald-50 text-emerald-700'
    : 'bg-black/[0.05] text-muted';

  return (
    <span className={`rounded-full px-3 py-1.5 text-[0.68rem] font-medium uppercase tracking-[0.12em] ${className}`}>
      {status}
    </span>
  );
}

function buildSessionsUrl(agentId: string | undefined, page: number): string {
  const params = new URLSearchParams();

  if (agentId) {
    params.set('agentId', agentId);
  }

  if (page > 1) {
    params.set('page', String(page));
  }

  const query = params.toString();
  return query.length > 0 ? `/sessions?${query}` : '/sessions';
}

function parsePositiveInteger(value: string | undefined): number | null {
  if (!value) {
    return null;
  }

  const parsed = Number.parseInt(value, 10);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}