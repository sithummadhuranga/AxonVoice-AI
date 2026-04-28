import Link from 'next/link';
import { notFound } from 'next/navigation';
import { AgentTestConsole } from '@/components/agent-test-console';
import { getAgent } from '@/lib/api';
import { requireConsoleSession } from '@/lib/console-session';

interface AgentTestPageProps {
  params: Promise<{ id: string }>;
}

export default async function AgentTestPage({ params }: AgentTestPageProps) {
  await requireConsoleSession();
  const { id } = await params;
  const agent = await getAgent(id);
  if (!agent) { notFound(); }

  return (
    <div className="grid gap-6">
      <section className="surface-card-strong relative overflow-hidden p-6 lg:p-8">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{
            background:
              'radial-gradient(circle at 12% 80%, rgba(20,115,230,0.10) 0%, transparent 30%), radial-gradient(circle at 88% 20%, rgba(255,122,69,0.10) 0%, transparent 28%)',
          }}
        />
        <div className="relative flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <Link
              href={`/agents/${id}`}
              className="eyebrow text-accent hover:underline"
            >
              ← {agent.displayName}
            </Link>
            <h1 className="mt-4 text-[2.4rem] font-semibold tracking-[-0.07em] text-foreground lg:text-[3rem]">
              Test console
            </h1>
            <p className="mt-3 max-w-2xl text-sm leading-7 text-muted">
              Start a live voice session with this agent directly from the dashboard. Uses your
              tenant&apos;s Gemini key and records session data in the live log.
            </p>
          </div>
          <span
            className={`flex items-center gap-1.5 rounded-full px-3 py-1.5 text-[0.68rem] font-semibold uppercase tracking-[0.12em] ${
              agent.isActive
                ? 'bg-emerald-50 text-emerald-700'
                : 'bg-black/[0.05] text-muted'
            }`}
          >
            <span
              className={`h-1.5 w-1.5 rounded-full ${
                agent.isActive ? 'bg-emerald-500' : 'bg-muted/40'
              }`}
            />
            {agent.isActive ? 'Active' : 'Paused'}
          </span>
        </div>
      </section>

      <AgentTestConsole
        agentId={agent.id}
        agentName={agent.displayName}
        isActive={agent.isActive}
        primaryLanguage={agent.primaryLanguage}
        geminiModel={agent.geminiModel}
        toolsEnabled={agent.toolsEnabled ?? []}
      />
    </div>
  );
}
