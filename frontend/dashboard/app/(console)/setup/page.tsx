import { ActionSubmitButton } from '@/components/action-submit-button';
import { MutationBanner } from '@/components/mutation-banner';
import Link from 'next/link';
import { getCurrentTenant, getErrorMessage, hasGeminiApiKeyConfigured } from '@/lib/api';
import { requireConsoleSession } from '@/lib/console-session';
import { updateTenantGeminiApiKey } from '@/lib/tenant-actions';

type SetupPageProps = {
  searchParams: Promise<{
    error?: string;
    message?: string;
  }>;
};

export default async function SetupPage({ searchParams }: SetupPageProps) {
  const session = await requireConsoleSession();
  const { error, message } = await searchParams;

  let tenant: Awaited<ReturnType<typeof getCurrentTenant>> | null = null;
  let loadError: string | null = null;

  try {
    tenant = await getCurrentTenant();
  } catch (cause) {
    loadError = getErrorMessage(cause);
  }

  const apiKeyConfigured = tenant ? hasGeminiApiKeyConfigured(tenant) : false;

  return (
    <div className="grid gap-6">
      <section className="surface-card-strong relative overflow-hidden p-6 lg:p-8">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{
            background:
              'radial-gradient(circle at 90% 12%, rgba(20,115,230,0.12) 0%, transparent 24%), radial-gradient(circle at 10% 86%, rgba(255,122,69,0.10) 0%, transparent 22%)',
          }}
        />
        <div className="relative grid gap-6 xl:grid-cols-[minmax(0,1fr)_18rem] xl:items-start">
          <div>
            <p className="eyebrow text-accent">Infrastructure</p>
            <h1 className="page-title mt-4 max-w-3xl text-foreground">
              Store the credential the relay will trust for live traffic.
            </h1>
            <p className="page-subtitle mt-5">
              The console accepts the tenant&apos;s Google AI Studio key, stores it encrypted on the backend, and never returns the plaintext credential after submission.
            </p>
          </div>

          <div className="rounded-[1.6rem] bg-foreground px-5 py-5 text-white shadow-[0_26px_50px_rgba(15,26,40,0.24)]">
            <p className="eyebrow text-white/50">Security posture</p>
            <div className="mt-4 grid gap-3">
              <SecurityItem label="Encrypted at rest" />
              <SecurityItem label="Server-side submission path" />
              <SecurityItem label="Not re-exposed to the browser" />
            </div>
          </div>
        </div>
      </section>

      {message ? <MutationBanner tone="success">{message}</MutationBanner> : null}
      {error ? <MutationBanner tone="error">{error}</MutationBanner> : null}
      {loadError ? <MutationBanner tone="warning">{loadError}</MutationBanner> : null}

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_24rem]">
        <section className="surface-card-strong p-6 lg:p-7">
          <p className="eyebrow text-muted">Tenant status</p>
          <h2 className="mt-3 text-2xl font-semibold tracking-[-0.05em] text-foreground">
            {tenant?.name ?? session.tenantName}
          </h2>
          <p className="mt-4 text-sm leading-7 text-muted">
            {apiKeyConfigured
              ? `A Gemini API key is already stored. The visible hint ends with ${tenant?.apiKeyHint}. Rotate it here whenever the tenant replaces their Google AI Studio credential.`
              : 'No Gemini API key is stored yet. Save it to enable live voice sessions for this tenant.'}
          </p>

          <dl className="mt-6 grid gap-3">
            <StatusTile label="Owner account" value={session.email} />
            <StatusTile
              label="Gemini API key"
              value={apiKeyConfigured ? 'Configured and ready for live sessions' : 'Not configured yet'}
            />
            <StatusTile
              label="Next step"
              value={apiKeyConfigured ? 'Review agents and move into live rollout' : 'Store the credential, then create or validate an agent'}
            />
          </dl>
        </section>

        <section className="surface-card p-6 lg:p-7">
          <p className="eyebrow text-muted">Credential form</p>
          <h2 className="mt-3 text-2xl font-semibold tracking-[-0.05em] text-foreground">
            {apiKeyConfigured ? 'Rotate API key' : 'Add Gemini API key'}
          </h2>
          <p className="mt-4 text-sm leading-7 text-muted">
            Generate a key in Google AI Studio, paste it below, and submit through the authenticated server path. The plaintext key is not returned after submission.
          </p>

          {tenant ? (
            <form action={updateTenantGeminiApiKey} className="mt-5 grid gap-4">
              <label className="grid gap-2">
                <span className="text-sm font-medium text-foreground">
                  {apiKeyConfigured ? 'New Gemini API key' : 'Gemini API key'}
                </span>
                <input
                  autoComplete="off"
                  className="h-12 rounded-[1rem] border border-line-strong bg-white/86 px-4 text-sm text-foreground shadow-[inset_0_1px_0_rgba(255,255,255,0.82)] outline-none transition placeholder:text-muted/60 focus:border-accent/40 focus:ring-4 focus:ring-accent-soft"
                  name="geminiApiKey"
                  placeholder="AIza..."
                  required
                  type="password"
                />
              </label>
              <ActionSubmitButton
                className="primary-button w-full disabled:cursor-wait disabled:opacity-80"
                idleLabel={apiKeyConfigured ? 'Rotate API key' : 'Save API key'}
                pendingLabel={apiKeyConfigured ? 'Saving key...' : 'Saving key...'}
              />
            </form>
          ) : (
            <div className="mt-5 rounded-[1.25rem] border border-dashed border-line px-4 py-5 text-sm text-muted">
              Resolve the tenant record before updating the Gemini API key.
            </div>
          )}

          <div className="mt-4 flex flex-wrap gap-3">
            <a
              href="https://aistudio.google.com/app/apikey"
              target="_blank"
              rel="noreferrer"
              className="secondary-button"
            >
              Open Google AI Studio ↗
            </a>
            {apiKeyConfigured ? (
              <Link href="/dashboard" className="secondary-button">
                Back to dashboard
              </Link>
            ) : null}
          </div>

          <div className="mt-5 rounded-[1.25rem] border border-line bg-white/72 px-4 py-4 text-sm leading-6 text-muted">
            The relay decrypts the stored key in memory only when opening a Gemini Live session for this tenant.
          </div>
        </section>
      </div>
    </div>
  );
}

function SecurityItem({ label }: { label: string }) {
  return (
    <div className="flex items-center gap-3 rounded-[1.1rem] bg-white/6 px-4 py-3 text-sm text-white/90">
      <span className="h-2 w-2 rounded-full bg-accent-warm" />
      <span>{label}</span>
    </div>
  );
}

function StatusTile({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[1.15rem] border border-line bg-white/78 px-4 py-3">
      <dt className="text-xs font-medium uppercase tracking-[0.12em] text-muted">{label}</dt>
      <dd className="mt-2 text-sm leading-6 text-foreground">{value}</dd>
    </div>
  );
}
