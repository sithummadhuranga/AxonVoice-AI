import Link from 'next/link';
import { getCurrentTenant, getDashboardOverview, hasGeminiApiKeyConfigured } from '@/lib/api';
import { requireConsoleSession } from '@/lib/console-session';

const LANGUAGE_LABELS: Record<string, string> = {
  si: 'Sinhala',
  ta: 'Tamil',
  en: 'English',
};

function languageLabel(code: string): string {
  return LANGUAGE_LABELS[code] ?? code;
}

export default async function DashboardPage() {
  await requireConsoleSession();
  const tenant = await getCurrentTenant();
  const needsSetup = !hasGeminiApiKeyConfigured(tenant);

  const overview = await getDashboardOverview();
  const activeShare =
    overview.configuredAgents > 0
      ? `${Math.round((overview.activeAgents / overview.configuredAgents) * 100)}% live-ready`
      : 'No agents configured';

  return (
    <div className="grid gap-6">
      {needsSetup ? (
        <div className="flex items-start gap-4 rounded-[1.5rem] border border-amber-200/80 bg-amber-50/90 px-5 py-4 shadow-[var(--shadow-sm)]">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-2xl bg-amber-100 text-amber-600">
            <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden="true">
              <path d="M9 2L16 15H2L9 2Z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round"/>
              <path d="M9 7v4M9 12.5v.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
            </svg>
          </div>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold tracking-[-0.02em] text-amber-900">Gemini API key required before going live</p>
            <p className="mt-1 text-sm leading-6 text-amber-700">
              Store your tenant&apos;s Gemini credential to enable voice session routing. Agents can be created now and activated after setup.
            </p>
          </div>
          <Link href="/setup" className="shrink-0 rounded-[0.9rem] bg-amber-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-amber-700">
            Set up now
          </Link>
        </div>
      ) : null}

      <section className="surface-card-strong relative overflow-hidden p-6 lg:p-8">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{
            background:
              'radial-gradient(circle at 18% 18%, rgba(20,115,230,0.10) 0%, transparent 30%), radial-gradient(circle at 92% 12%, rgba(255,122,69,0.12) 0%, transparent 24%)',
          }}
        />
        <div className="relative grid gap-6 xl:grid-cols-[minmax(0,1fr)_20rem] xl:items-start">
          <div>
            <p className="eyebrow text-accent">Operator Overview</p>
            <h1 className="page-title mt-4 max-w-3xl text-foreground">
              Operate multilingual voice traffic with live visibility.
            </h1>
            <p className="page-subtitle mt-5">
              Review rollout readiness, track which agents can take traffic now, and keep the tenant&apos;s live
              configuration path visible from one control surface.
            </p>
            <div className="mt-7 flex flex-wrap gap-3">
              <Link href="/agents" className="primary-button">
                Open agent inventory
              </Link>
              <Link href="/setup" className="secondary-button">
                Inspect infrastructure
              </Link>
            </div>
          </div>

          <div className="grid gap-3">
            <div className="rounded-[1.6rem] border border-line bg-white/82 p-5 shadow-[var(--shadow-md)]">
              <div className="flex items-center justify-between gap-3">
                <p className="text-sm font-semibold tracking-[-0.02em] text-foreground">Gateway reachability</p>
                <StatusBadge status={overview.status} />
              </div>
              <p className="mt-3 text-sm leading-6 text-muted">
                {overview.message ?? 'The console can reach the gateway-backed agent configuration API.'}
              </p>
            </div>

            <div className="rounded-[1.6rem] bg-foreground px-5 py-5 text-white shadow-[0_26px_50px_rgba(15,26,40,0.24)]">
              <p className="eyebrow text-white/50">Current footprint</p>
              <p className="mt-4 text-3xl font-semibold tracking-[-0.06em]">
                {overview.languages.length === 0 ? 'No language coverage yet' : `${overview.languages.length} language paths live`}
              </p>
              <div className="mt-4 flex flex-wrap gap-2">
                {overview.languages.length > 0 ? (
                  overview.languages.map((language) => (
                    <span
                      key={language}
                      className="rounded-full border border-white/10 bg-white/8 px-3 py-1.5 text-xs font-medium uppercase tracking-[0.12em] text-white/88"
                    >
                      {languageLabel(language)}
                    </span>
                  ))
                ) : (
                  <span className="rounded-full border border-white/10 bg-white/8 px-3 py-1.5 text-xs font-medium uppercase tracking-[0.12em] text-white/72">
                    Create the first agent to begin rollout
                  </span>
                )}
              </div>
            </div>
          </div>
        </div>
      </section>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <MetricCard
          detail="Current workspace inventory"
          label="Configured agents"
          value={overview.configuredAgents.toString()}
          icon={
            <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden="true">
              <circle cx="9" cy="6" r="3" stroke="currentColor" strokeWidth="1.5"/>
              <path d="M3 15c0-3.314 2.686-6 6-6s6 2.686 6 6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
            </svg>
          }
        />
        <MetricCard
          detail={activeShare}
          label="Active agents"
          value={overview.activeAgents.toString()}
          icon={
            <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden="true">
              <circle cx="9" cy="9" r="3" fill="currentColor" className="opacity-30"/>
              <circle cx="9" cy="9" r="6" stroke="currentColor" strokeWidth="1.5"/>
              <path d="M9 6v3l2 2" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
            </svg>
          }
          highlight
        />
        <MetricCard
          detail={overview.languages.map(languageLabel).join(' · ') || 'No language coverage yet'}
          label="Languages covered"
          value={overview.languages.length.toString()}
          icon={
            <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden="true">
              <circle cx="9" cy="9" r="7" stroke="currentColor" strokeWidth="1.5"/>
              <path d="M9 2c-2.5 3-2.5 11 0 14M9 2c2.5 3 2.5 11 0 14M2 9h14" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round"/>
            </svg>
          }
        />
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
        <section className="surface-card-strong overflow-hidden p-5 lg:p-6">
          <div className="flex items-center justify-between gap-4 border-b border-line pb-4">
            <div>
              <p className="eyebrow text-muted">Recent agents</p>
              <h2 className="mt-2 text-lg font-semibold tracking-[-0.03em] text-foreground">
                Latest live contracts
              </h2>
            </div>
            <Link href="/agents" className="secondary-button">
              View all agents
            </Link>
          </div>

          {overview.agents.length === 0 ? (
            <div className="flex flex-col items-center gap-4 px-4 py-14 text-center">
              <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-accent-soft text-accent">
                <svg width="22" height="22" viewBox="0 0 22 22" fill="none" aria-hidden="true">
                  <circle cx="11" cy="8" r="4" stroke="currentColor" strokeWidth="1.5"/>
                  <path d="M4 19c0-3.866 3.134-7 7-7s7 3.134 7 7" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
                </svg>
              </div>
              <div>
                <p className="text-base font-semibold tracking-[-0.02em] text-foreground">No agents configured yet</p>
                <p className="mt-2 text-sm leading-6 text-muted">
                  {overview.message ?? 'Create the first voice contract to move this tenant into live operations.'}
                </p>
              </div>
              <Link href="/agents/new" className="primary-button">
                Create first agent
              </Link>
            </div>
          ) : (
            <ul className="mt-4 grid gap-3">
              {overview.agents.slice(0, 5).map((agent) => (
                <li key={agent.id}>
                  <Link
                    href={`/agents/${agent.id}`}
                    className="group flex items-center gap-4 rounded-[1.45rem] border border-line bg-white/76 px-4 py-4 transition hover:-translate-y-0.5 hover:border-accent/20 hover:bg-white/90"
                  >
                    <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-accent-soft text-sm font-semibold text-accent">
                      {agent.displayName.charAt(0).toUpperCase()}
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-semibold tracking-[-0.02em] text-foreground">
                        {agent.displayName}
                      </p>
                      <p className="mt-1 truncate text-xs text-muted">{agent.name}</p>
                    </div>
                    <div className="hidden shrink-0 items-center gap-2 md:flex">
                      <span className="rounded-full border border-line bg-white/82 px-3 py-1.5 text-[0.68rem] font-medium uppercase tracking-[0.12em] text-muted">
                        {languageLabel(agent.primaryLanguage)}
                      </span>
                      <span
                        className={`rounded-full px-3 py-1.5 text-[0.68rem] font-medium uppercase tracking-[0.12em] ${
                          agent.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-black/[0.05] text-muted'
                        }`}
                      >
                        {agent.isActive ? 'Active' : 'Paused'}
                      </span>
                    </div>
                    <svg
                      width="16"
                      height="16"
                      viewBox="0 0 16 16"
                      fill="none"
                      aria-hidden="true"
                      className="shrink-0 text-muted transition group-hover:text-accent"
                    >
                      <path d="M6 3.5L10.5 8L6 12.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                    </svg>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>

        <div className="grid gap-4">
          <section className="surface-card p-5">
            <p className="eyebrow text-muted">Go-live checklist</p>
            <h2 className="mt-2 text-lg font-semibold tracking-[-0.03em] text-foreground">Operational runway</h2>
            <div className="mt-4 grid gap-3">
              {[
                { done: true, text: 'Gemini API key stored and encrypted.' },
                { done: overview.configuredAgents > 0, text: 'At least one agent contract has been defined.' },
                { done: false, text: 'Business hours and slot capacity are still pending.' },
                { done: false, text: 'Knowledge documents still need to be ingested.' },
              ].map((item) => (
                <ChecklistRow key={item.text} done={item.done} text={item.text} />
              ))}
            </div>
          </section>

          <section className="surface-card p-5">
            <p className="eyebrow text-muted">Operator shortcuts</p>
            <h2 className="mt-2 text-lg font-semibold tracking-[-0.03em] text-foreground">Next moves</h2>
            <div className="mt-4 grid gap-3">
              <Link href="/agents/new" className="primary-button w-full">
                Create next agent
              </Link>
              <Link href="/setup" className="secondary-button w-full justify-center">
                Review credential handling
              </Link>
            </div>
          </section>
        </div>
      </div>
    </div>
  );
}

function MetricCard({
  detail,
  highlight,
  icon,
  label,
  value,
}: {
  detail: string;
  highlight?: boolean;
  icon: React.ReactNode;
  label: string;
  value: string;
}) {
  return (
    <div
      className={`surface-card p-5 ${
        highlight ? 'border-accent/18 bg-[linear-gradient(135deg,rgba(20,115,230,0.10),rgba(255,255,255,0.84))]' : ''
      }`}
    >
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-sm font-semibold tracking-[-0.02em] text-foreground">{label}</p>
          <p className="mt-1 text-sm leading-6 text-muted">{detail}</p>
        </div>
        <span className={highlight ? 'text-accent' : 'text-muted'}>{icon}</span>
      </div>
      <p className={`mt-5 text-4xl font-semibold tracking-[-0.06em] ${highlight ? 'text-accent' : 'text-foreground'}`}>
        {value}
      </p>
    </div>
  );
}

function ChecklistRow({ done, text }: { done: boolean; text: string }) {
  return (
    <div className="flex items-start gap-3 rounded-[1.15rem] border border-line bg-white/76 px-4 py-3">
      <span
        className={`mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full ${
          done ? 'bg-emerald-100 text-emerald-600' : 'border border-line bg-white text-transparent'
        }`}
      >
        <svg width="9" height="9" viewBox="0 0 9 9" fill="none" aria-hidden="true">
          <path d="M1.5 4.5L3.4 6.4L7.2 2.5" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round"/>
        </svg>
      </span>
      <span className={done ? 'text-sm text-foreground' : 'text-sm text-muted'}>{text}</span>
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const isReady = status === 'ready';
  return (
    <span
      className={`flex items-center gap-1.5 rounded-full px-3 py-1.5 text-[0.68rem] font-semibold uppercase tracking-[0.12em] ${
        isReady ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700'
      }`}
    >
      <span className={`h-1.5 w-1.5 rounded-full ${isReady ? 'bg-emerald-500' : 'bg-amber-500'}`} />
      {isReady ? 'API connected' : 'API unavailable'}
    </span>
  );
}