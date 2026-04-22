import { redirect } from 'next/navigation';
import { loginConsoleUser, registerConsoleOwner } from '@/lib/console-auth-actions';
import { getConsoleSession } from '@/lib/console-session';

type LoginPageProps = {
  searchParams: Promise<{
    error?: string;
    mode?: string;
  }>;
};

export default async function LoginPage({ searchParams }: LoginPageProps) {
  const session = await getConsoleSession();
  if (session) {
    redirect('/dashboard');
  }

  const { error, mode } = await searchParams;
  const registerSelected = mode === 'register';

  return (
    <section className="grid gap-6">
      <header className="rounded-4xl border border-line bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)]">
        <p className="font-mono text-[0.74rem] uppercase tracking-[0.28em] text-accent">
          Console access
        </p>
        <h2 className="mt-4 text-4xl font-semibold tracking-[-0.07em] text-foreground">
          Secure tenant onboarding and operator sign-in.
        </h2>
        <p className="mt-3 max-w-2xl text-sm leading-6 text-muted">
          The dashboard keeps operator tokens in an HttpOnly cookie and sends them to the gateway only from the server.
        </p>
      </header>

      {error ? (
        <section className="rounded-[1.75rem] border border-dashed border-line px-6 py-5 text-sm leading-6 text-muted">
          {error}
        </section>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-2">
        <section className={`rounded-4xl border bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)] ${registerSelected ? 'border-line/50' : 'border-line'}`}>
          <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-accent">Sign in</p>
          <h3 className="mt-4 text-2xl font-semibold tracking-[-0.05em] text-foreground">
            Access an existing tenant workspace.
          </h3>
          <form action={loginConsoleUser} className="mt-6 grid gap-4">
            <FormField label="Email" name="email" type="email" autoComplete="email" />
            <FormField label="Password" name="password" type="password" autoComplete="current-password" />
            <button
              type="submit"
              className="mt-2 rounded-full bg-sidebar px-5 py-3 text-sm font-semibold text-sidebar-foreground transition hover:bg-black"
            >
              Sign in
            </button>
          </form>
        </section>

        <section className={`rounded-4xl border bg-panel p-8 shadow-[0_18px_60px_rgba(49,34,21,0.08)] ${registerSelected ? 'border-line' : 'border-line/50'}`}>
          <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-accent">Create account</p>
          <h3 className="mt-4 text-2xl font-semibold tracking-[-0.05em] text-foreground">
            Start a new tenant and owner account.
          </h3>
          <p className="mt-3 text-sm leading-6 text-muted">
            Use a password with at least 12 characters, including uppercase, lowercase, and numeric characters.
          </p>
          <form action={registerConsoleOwner} className="mt-6 grid gap-4">
            <FormField label="Business name" name="businessName" type="text" autoComplete="organization" />
            <FormField label="Owner email" name="email" type="email" autoComplete="email" />
            <FormField label="Password" name="password" type="password" autoComplete="new-password" />
            <button
              type="submit"
              className="mt-2 rounded-full border border-line bg-white/80 px-5 py-3 text-sm font-semibold text-foreground transition hover:bg-white"
            >
              Create account
            </button>
          </form>
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