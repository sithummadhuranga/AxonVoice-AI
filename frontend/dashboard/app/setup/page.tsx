import Link from 'next/link';
import { getCurrentTenant, getErrorMessage, hasGeminiApiKeyConfigured } from '@/lib/api';
import { requireConsoleSession } from '@/lib/console-session';
import { updateTenantGeminiApiKey } from '@/lib/tenant-actions';

type SetupPageProps = {
  searchParams: Promise<{
    error?: string;
  }>;
};

export default async function SetupPage({ searchParams }: SetupPageProps) {
  const session = await requireConsoleSession();
  const { error } = await searchParams;

  let tenant: Awaited<ReturnType<typeof getCurrentTenant>> | null = null;
  let loadError: string | null = null;

  try {
    tenant = await getCurrentTenant();
  } catch (cause) {
    loadError = getErrorMessage(cause);
  }

  const apiKeyConfigured = tenant ? hasGeminiApiKeyConfigured(tenant) : false;

  return (
    <section className="grid gap-6">
      <header className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
        <p className="font-mono text-[0.74rem] uppercase tracking-[0.28em] text-accent">
          Setup
        </p>
        <h2 className="mt-4 text-4xl font-semibold tracking-[-0.07em] text-foreground">
          Connect the tenant Gemini API key before live voice traffic starts.
        </h2>
        <p className="mt-3 max-w-3xl text-sm leading-6 text-muted">
          AxonVoice uses the tenant&apos;s own Google AI Studio key only for live session relay traffic. The key is encrypted on the backend before it is stored and never returned to the dashboard after submission.
        </p>
      </header>

      {error ? <MessagePanel message={error} /> : null}
      {loadError ? <MessagePanel message={loadError} /> : null}

      <div className="grid gap-6 lg:grid-cols-[1.05fr_0.95fr]">
        <section className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
          <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-accent">
            Tenant status
          </p>
          <h3 className="mt-4 text-3xl font-semibold tracking-[-0.06em] text-foreground">
            {tenant?.name ?? session.tenantName}
          </h3>
          <p className="mt-3 text-sm leading-6 text-muted">
            {apiKeyConfigured
              ? `A Gemini API key is already stored. The current hint ends with ${tenant?.apiKeyHint}. Rotate it here whenever the tenant replaces their Google AI Studio credential.`
              : 'No Gemini API key is stored yet. Save it now so the relay can open Gemini Live sessions for this tenant.'}
          </p>

          <div className="mt-6 grid gap-3">
            <StatusRow
              label="Owner account"
              value={`${session.email} is authenticated for tenant-scoped setup.`}
            />
            <StatusRow
              label="Gemini key"
              value={apiKeyConfigured
                ? 'Stored securely and ready for live voice sessions.'
                : 'Pending. Live voice traffic should stay disabled until this step is complete.'}
            />
            <StatusRow
              label="Next milestone"
              value="Create the first voice agent after the Gemini API key is saved."
            />
          </div>
        </section>

        <section className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
          <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-accent">
            {apiKeyConfigured ? 'Rotate key' : 'Add Gemini API key'}
          </p>
          <h3 className="mt-4 text-2xl font-semibold tracking-[-0.05em] text-foreground">
            Store the credential used for multilingual live voice sessions.
          </h3>
          <p className="mt-3 text-sm leading-6 text-muted">
            Generate a browser-safe API key in Google AI Studio, then paste it here. Submission happens server-side and the dashboard keeps using an HttpOnly session cookie rather than a browser-stored bearer token.
          </p>

          {tenant ? (
            <form action={updateTenantGeminiApiKey} className="mt-6 grid gap-4">
              <FormField
                autoComplete="off"
                label={apiKeyConfigured ? 'New Gemini API key' : 'Gemini API key'}
                name="geminiApiKey"
                type="password"
              />
              <button
                type="submit"
                className="mt-2 rounded-full bg-sidebar px-5 py-3 text-sm font-semibold text-sidebar-foreground transition hover:bg-black"
              >
                {apiKeyConfigured ? 'Rotate API key' : 'Save API key'}
              </button>
            </form>
          ) : (
            <div className="mt-6 rounded-3xl border border-dashed border-line px-5 py-6 text-sm leading-6 text-muted">
              Resolve the tenant record before updating the Gemini API key.
            </div>
          )}

          <div className="mt-4 flex flex-wrap gap-3">
            <a
              href="https://aistudio.google.com/app/apikey"
              target="_blank"
              rel="noreferrer"
              className="rounded-full border border-line bg-white/80 px-4 py-2 text-sm text-foreground transition hover:bg-white"
            >
              Open Google AI Studio
            </a>
            {apiKeyConfigured ? (
              <Link
                href="/dashboard"
                className="rounded-full border border-line bg-white/80 px-4 py-2 text-sm text-foreground transition hover:bg-white"
              >
                Return to dashboard
              </Link>
            ) : null}
          </div>
        </section>
      </div>
    </section>
  );
}

function FormField({
  autoComplete,
  label,
  name,
  type,
}: {
  autoComplete?: string;
  label: string;
  name: string;
  type: string;
}) {
  return (
    <label className="grid gap-2 text-sm text-muted">
      <span>{label}</span>
      <input
        autoComplete={autoComplete}
        className="rounded-[1.25rem] border border-line bg-white/80 px-4 py-3 text-sm text-foreground outline-none"
        name={name}
        required
        type={type}
      />
    </label>
  );
}

function MessagePanel({ message }: { message: string }) {
  return (
    <section className="rounded-[1.75rem] border border-dashed border-line px-6 py-5 text-sm leading-6 text-muted">
      {message}
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