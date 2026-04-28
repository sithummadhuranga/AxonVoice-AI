import { ActionSubmitButton } from '@/components/action-submit-button';
import { MutationBanner } from '@/components/mutation-banner';
import Link from 'next/link';
import { getAgents, getErrorMessage, getKnowledgeDocuments, type AgentSummary, type KnowledgeDocumentStatus, type KnowledgeDocumentSummary } from '@/lib/api';
import { uploadKnowledgeDocumentAction, deleteKnowledgeDocumentAction } from '@/lib/knowledge-actions';
import { formatConsoleDateTime } from '@/lib/console-format';
import { requireConsoleSession } from '@/lib/console-session';

type KnowledgeBasePageProps = {
  searchParams: Promise<{
    agentId?: string;
    error?: string;
    message?: string;
  }>;
};

const languageOptions = [
  { value: '', label: 'Detect from content' },
  { value: 'si', label: 'Sinhala' },
  { value: 'ta', label: 'Tamil' },
  { value: 'en', label: 'English' },
];

export default async function KnowledgeBasePage({ searchParams }: KnowledgeBasePageProps) {
  await requireConsoleSession();

  const [{ agentId: requestedAgentId, error, message }, agents] = await Promise.all([
    searchParams,
    getAgents().catch(() => []),
  ]);

  if (agents.length === 0) {
    return <EmptyAgentState title="Knowledge Base" description="Create an agent before uploading documents for retrieval-backed voice sessions." />;
  }

  const selectedAgent = resolveSelectedAgent(agents, requestedAgentId);

  let documents: KnowledgeDocumentSummary[] = [];
  let loadError: string | null = null;

  try {
    documents = await getKnowledgeDocuments(selectedAgent.id);
  } catch (cause) {
    loadError = getErrorMessage(cause);
  }

  const readyDocuments = documents.filter((document) => document.status === 'ready').length;
  const processingDocuments = documents.filter((document) => isKnowledgeDocumentPending(document.status)).length;

  return (
    <div className="grid gap-6">
      <section className="surface-card-strong relative overflow-hidden p-6 lg:p-8">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{
            background:
              'radial-gradient(circle at 88% 14%, rgba(20,115,230,0.12) 0%, transparent 24%), radial-gradient(circle at 8% 84%, rgba(255,122,69,0.10) 0%, transparent 22%)',
          }}
        />
        <div className="relative grid gap-6 xl:grid-cols-[minmax(0,1fr)_18rem] xl:items-start">
          <div>
            <p className="eyebrow text-accent">Knowledge Base</p>
            <h1 className="page-title mt-4 max-w-3xl text-foreground">
              Feed the retrieval layer with the documents each voice agent actually needs in production.
            </h1>
            <p className="page-subtitle mt-5">
              Upload PDFs, DOCX files, or plaintext references, monitor ingestion status, and remove outdated context before it leaks into live sessions.
            </p>
            <div className="mt-7 flex flex-wrap gap-3">
              <Link href="/dashboard" className="secondary-button">
                Return to overview
              </Link>
              <Link href={buildKnowledgeBaseUrl(selectedAgent.id)} className="secondary-button">
                Refresh documents
              </Link>
            </div>
          </div>

          <div className="grid gap-3">
            <SummaryCard label="Visible documents" value={documents.length.toString()} />
            <SummaryCard label="Ready" value={readyDocuments.toString()} />
            <SummaryCard label="Processing" value={processingDocuments.toString()} />
          </div>
        </div>
      </section>

      {error ? <MutationBanner tone="error">{error}</MutationBanner> : null}
      {message ? <MutationBanner tone="success">{message}</MutationBanner> : null}
      {loadError ? <MutationBanner tone="warning">{loadError}</MutationBanner> : null}

      <section className="surface-card-strong overflow-hidden p-5 lg:p-6">
        <div className="flex flex-col gap-4 border-b border-line pb-4 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <p className="eyebrow text-muted">Agent filter</p>
            <h2 className="mt-2 text-lg font-semibold tracking-[-0.03em] text-foreground">
              Retrieval scope
            </h2>
          </div>
          <div className="flex flex-wrap gap-2">
            {agents.map((agent) => (
              <FilterChip key={agent.id} href={buildKnowledgeBaseUrl(agent.id)} active={agent.id === selectedAgent.id}>
                {agent.displayName}
              </FilterChip>
            ))}
          </div>
        </div>

        <div className="mt-5 grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
          <section className="rounded-[1.5rem] border border-line bg-white/84 p-5 shadow-[var(--shadow-sm)]">
            <p className="eyebrow text-muted">Upload form</p>
            <h2 className="mt-3 text-2xl font-semibold tracking-[-0.05em] text-foreground">
              Queue a new document
            </h2>
            <p className="mt-4 text-sm leading-7 text-muted">
              The upload returns immediately and the ingestion worker continues chunking and embedding in the background.
            </p>

            <form action={uploadKnowledgeDocumentAction} className="mt-5 grid gap-4">
              <input type="hidden" name="agentId" value={selectedAgent.id} />
              <label className="grid gap-2">
                <span className="text-sm font-medium text-foreground">Document file</span>
                <input
                  required
                  type="file"
                  name="file"
                  accept=".pdf,.docx,.txt,text/plain,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                  className="rounded-[1rem] border border-line-strong bg-white/86 px-4 py-3 text-sm text-foreground shadow-[inset_0_1px_0_rgba(255,255,255,0.82)] outline-none transition focus:border-accent/40 focus:ring-4 focus:ring-accent-soft"
                />
              </label>
              <label className="grid gap-2">
                <span className="text-sm font-medium text-foreground">Language hint</span>
                <select
                  name="language"
                  className="h-12 rounded-[1rem] border border-line-strong bg-white/86 px-4 text-sm text-foreground shadow-[inset_0_1px_0_rgba(255,255,255,0.82)] outline-none transition focus:border-accent/40 focus:ring-4 focus:ring-accent-soft"
                  defaultValue=""
                >
                  {languageOptions.map((option) => (
                    <option key={option.label} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </label>
              <ActionSubmitButton
                className="primary-button w-full disabled:cursor-wait disabled:opacity-80"
                idleLabel="Upload document"
                pendingLabel="Uploading..."
              />
            </form>
          </section>

          <section className="surface-card p-5">
            <p className="eyebrow text-muted">Operational notes</p>
            <div className="mt-4 grid gap-3 text-sm leading-6 text-muted">
              <InfoCard label="Allowed formats" value="PDF, DOCX, and plain text" />
              <InfoCard label="Duplicate detection" value="Content hashes block identical uploads for the same agent" />
              <InfoCard label="Ingestion model" value="Uploads are chunked and embedded asynchronously after acceptance" />
            </div>
          </section>
        </div>
      </section>

      <section className="surface-card-strong overflow-hidden p-5 lg:p-6">
        <div className="border-b border-line pb-4">
          <p className="eyebrow text-muted">Document list</p>
          <h2 className="mt-2 text-lg font-semibold tracking-[-0.03em] text-foreground">
            Current retrieval corpus
          </h2>
        </div>

        {documents.length === 0 ? (
          <div className="px-4 py-12 text-sm text-muted">
            No knowledge documents have been uploaded for this agent yet.
          </div>
        ) : (
          <div className="mt-4 grid gap-4">
            {documents.map((document) => (
              <article key={document.id} className="rounded-[1.5rem] border border-line bg-white/82 p-5 shadow-[var(--shadow-sm)]">
                <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="truncate text-base font-semibold tracking-[-0.02em] text-foreground">{document.filename}</h3>
                      <DocumentStatusChip status={document.status} />
                    </div>
                    <p className="mt-2 text-sm text-muted">
                      Uploaded {formatConsoleDateTime(document.uploadedAt)}
                    </p>
                    <p className="mt-2 text-sm text-muted">
                      {typeof document.chunkCount === 'number' ? `${document.chunkCount} chunks indexed` : 'Chunk count pending'}
                    </p>
                  </div>

                  <form action={deleteKnowledgeDocumentAction}>
                    <input type="hidden" name="agentId" value={selectedAgent.id} />
                    <input type="hidden" name="documentId" value={document.id} />
                    <ActionSubmitButton
                      className="secondary-button h-12 whitespace-nowrap disabled:cursor-wait disabled:opacity-80"
                      idleLabel="Delete document"
                      pendingLabel="Deleting..."
                    />
                  </form>
                </div>
              </article>
            ))}
          </div>
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

function InfoCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[1.15rem] border border-line bg-white/78 px-4 py-3">
      <p className="text-xs font-medium uppercase tracking-[0.12em] text-muted">{label}</p>
      <p className="mt-2 text-sm leading-6 text-foreground">{value}</p>
    </div>
  );
}

function DocumentStatusChip({ status }: { status: KnowledgeDocumentStatus }) {
  const className = status === 'ready'
    ? 'bg-emerald-50 text-emerald-700'
    : status === 'error'
      ? 'bg-red-50 text-red-700'
      : 'bg-amber-50 text-amber-700';

  return (
    <span className={`rounded-full px-3 py-1.5 text-[0.68rem] font-medium uppercase tracking-[0.12em] ${className}`}>
      {status}
    </span>
  );
}

function isKnowledgeDocumentPending(status: KnowledgeDocumentStatus): boolean {
  return status === 'uploading' || status === 'processing';
}

function EmptyAgentState({ description, title }: { description: string; title: string }) {
  return (
    <div className="grid gap-6">
      <section className="surface-card-strong p-6 lg:p-8">
        <p className="eyebrow text-accent">{title}</p>
        <h1 className="page-title mt-4 max-w-3xl text-foreground">No agent is ready for this operator workflow yet.</h1>
        <p className="page-subtitle mt-5">{description}</p>
        <div className="mt-7">
          <Link href="/agents/new" className="primary-button">
            Create agent
          </Link>
        </div>
      </section>
    </div>
  );
}

function resolveSelectedAgent(agents: AgentSummary[], requestedAgentId: string | undefined): AgentSummary {
  return agents.find((agent) => agent.id === requestedAgentId) ?? agents[0];
}

function buildKnowledgeBaseUrl(agentId: string): string {
  return `/knowledge-base?${new URLSearchParams({ agentId }).toString()}`;
}