import Link from 'next/link';
import { notFound } from 'next/navigation';
import { getAgent, getSession } from '@/lib/api';
import { getLanguageLabel } from '@/lib/agent-form';
import { formatConsoleDateTime, formatDurationSeconds, maskContactValue } from '@/lib/console-format';
import { requireConsoleSession } from '@/lib/console-session';

interface SessionDetailPageProps {
  params: Promise<{ id: string }>;
}

export default async function SessionDetailPage({ params }: SessionDetailPageProps) {
  await requireConsoleSession();

  const { id } = await params;
  const session = await getSession(id);
  if (!session) {
    notFound();
  }

  const agent = await getAgent(session.agentId).catch(() => null);
  const functionCalls = [...session.functionCalls].sort((left, right) =>
    Date.parse(left.calledAt) - Date.parse(right.calledAt));

  return (
    <div className="grid gap-6">
      <section className="surface-card-strong relative overflow-hidden p-6 lg:p-8">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{
            background:
              'radial-gradient(circle at 88% 14%, rgba(20,115,230,0.12) 0%, transparent 24%), radial-gradient(circle at 8% 86%, rgba(255,122,69,0.10) 0%, transparent 22%)',
          }}
        />
        <div className="relative flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <p className="eyebrow text-accent">Session inspection</p>
            <h1 className="mt-4 text-[2.25rem] font-semibold tracking-[-0.07em] text-foreground lg:text-[2.9rem]">
              {agent?.displayName ?? 'Unknown agent'}
            </h1>
            <p className="mt-3 max-w-3xl text-sm leading-7 text-muted">
              Session {session.id} started {formatConsoleDateTime(session.startedAt)} and is currently {session.status}.
            </p>
            <div className="mt-5 flex flex-wrap gap-2">
              <Chip>{getLanguageLabel(session.language)}</Chip>
              <Chip>{formatDurationSeconds(session.durationSeconds)}</Chip>
              <Chip>{maskContactValue(session.callerIdentifier)}</Chip>
            </div>
          </div>

          <div className="flex flex-wrap gap-3">
            <StatusChip status={session.status} />
            <Link href="/sessions" className="secondary-button">
              Back to sessions
            </Link>
          </div>
        </div>
      </section>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="grid gap-6">
          <section className="surface-card-strong p-6 lg:p-7">
            <p className="eyebrow text-muted">Session summary</p>
            <h2 className="mt-3 text-2xl font-semibold tracking-[-0.05em] text-foreground">
              {session.summary ?? 'Summary not recorded yet'}
            </h2>
            <p className="mt-4 text-sm leading-7 text-muted">
              {session.summary
                ? 'This summary was stored when the session closed.'
                : 'The relay has not stored a post-session summary for this call yet. Function-call telemetry is still available below.'}
            </p>
          </section>

          <section className="surface-card-strong overflow-hidden p-5 lg:p-6">
            <div className="border-b border-line pb-4">
              <p className="eyebrow text-muted">Tool activity</p>
              <h2 className="mt-2 text-lg font-semibold tracking-[-0.03em] text-foreground">
                Function-call trace
              </h2>
            </div>

            {functionCalls.length === 0 ? (
              <div className="px-4 py-12 text-sm text-muted">
                No function calls were recorded for this session.
              </div>
            ) : (
              <div className="mt-4 grid gap-4">
                {functionCalls.map((functionCall) => (
                  <article key={functionCall.id} className="rounded-[1.45rem] border border-line bg-white/82 p-4 shadow-[var(--shadow-sm)]">
                    <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
                      <div>
                        <div className="flex flex-wrap items-center gap-2">
                          <h3 className="text-base font-semibold tracking-[-0.02em] text-foreground">
                            {functionCall.functionName}
                          </h3>
                          <span className={`rounded-full px-3 py-1.5 text-[0.68rem] font-medium uppercase tracking-[0.12em] ${functionCall.succeeded ? 'bg-emerald-50 text-emerald-700' : 'bg-red-50 text-red-700'}`}>
                            {functionCall.succeeded ? 'Succeeded' : 'Failed'}
                          </span>
                        </div>
                        <p className="mt-2 text-sm text-muted">
                          Called {formatConsoleDateTime(functionCall.calledAt)} · {functionCall.durationMs} ms
                        </p>
                      </div>
                      {functionCall.errorMessage ? (
                        <span className="rounded-[1rem] border border-red-200/80 bg-red-50/90 px-3 py-2 text-sm text-red-700">
                          {functionCall.errorMessage}
                        </span>
                      ) : null}
                    </div>

                    <div className="mt-4 grid gap-4 xl:grid-cols-2">
                      <JsonPanel label="Arguments" value={functionCall.argumentsJson} />
                      <JsonPanel label="Result" value={functionCall.resultJson} />
                    </div>
                  </article>
                ))}
              </div>
            )}
          </section>
        </div>

        <aside className="grid gap-4 self-start xl:sticky xl:top-4">
          <section className="surface-card p-5">
            <p className="eyebrow text-muted">Session metadata</p>
            <div className="mt-4 grid gap-3">
              <InfoRow label="Session ID" value={session.id} mono />
              <InfoRow label="Agent" value={agent?.displayName ?? session.agentId} />
              <InfoRow label="Started at" value={formatConsoleDateTime(session.startedAt)} />
              <InfoRow label="Ended at" value={formatConsoleDateTime(session.endedAt)} />
              <InfoRow label="Duration" value={formatDurationSeconds(session.durationSeconds)} />
            </div>
          </section>

          <section className="surface-card p-5">
            <p className="eyebrow text-muted">Traffic posture</p>
            <div className="mt-4 grid gap-3">
              <InfoRow label="Status" value={session.status} />
              <InfoRow label="Language" value={getLanguageLabel(session.language)} />
              <InfoRow label="Caller" value={maskContactValue(session.callerIdentifier)} />
              <InfoRow label="Function calls" value={functionCalls.length.toString()} />
            </div>
          </section>
        </aside>
      </div>
    </div>
  );
}

function JsonPanel({ label, value }: { label: string; value: string | null }) {
  return (
    <section className="rounded-[1.25rem] border border-line bg-[rgba(245,247,250,0.94)] p-4">
      <p className="text-xs font-medium uppercase tracking-[0.12em] text-muted">{label}</p>
      <pre className="mt-3 overflow-x-auto whitespace-pre-wrap break-words text-xs leading-6 text-foreground">
        {formatJson(value)}
      </pre>
    </section>
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

function StatusChip({ status }: { status: string }) {
  const className = status === 'active'
    ? 'bg-emerald-50 text-emerald-700'
    : 'bg-black/[0.05] text-muted';

  return (
    <span className={`flex items-center gap-1.5 rounded-full px-3 py-1.5 text-[0.68rem] font-semibold uppercase tracking-[0.12em] ${className}`}>
      <span className={`h-1.5 w-1.5 rounded-full ${status === 'active' ? 'bg-emerald-500' : 'bg-muted/40'}`} />
      {status}
    </span>
  );
}

function formatJson(value: string | null): string {
  if (!value) {
    return 'Not recorded';
  }

  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}