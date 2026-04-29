import Link from 'next/link';
import { redirect } from 'next/navigation';
import { loginConsoleUser, registerConsoleOwner } from '@/lib/console-auth-actions';
import { getConsoleSession } from '@/lib/console-session';
import { AuthPasswordField } from '@/components/auth-password-field';

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
    <div className="grid h-full gap-4 sm:gap-5">
      <div>
        <p className="eyebrow text-accent">Operator Access</p>
        <h2 className="mt-3 text-[1.8rem] font-semibold leading-[0.98] tracking-[-0.06em] text-foreground sm:text-[2.1rem]">
          {registerSelected ? 'Create workspace' : 'Welcome back'}
        </h2>
        <p className="mt-2 text-sm leading-6 text-muted">
          {registerSelected
            ? 'Create the owner account, secure credentials, and bring the first voice agent online.'
            : 'Sign in to manage agent rollout, tenant secrets, and live voice operations.'}
        </p>
      </div>

      <div className="grid grid-cols-2 rounded-[1.35rem] border border-line bg-white/72 p-1.5 shadow-[inset_0_1px_0_rgba(255,255,255,0.72)]">
        <Link
          href="/login"
          className={`group inline-flex items-center justify-center rounded-[1rem] px-4 py-2.5 text-sm font-semibold transition ${
            !registerSelected
              ? 'bg-foreground shadow-[0_16px_24px_rgba(15,26,40,0.16)]'
              : 'hover:bg-black/[0.02]'
          }`}
        >
          <span className={!registerSelected ? 'text-sidebar-foreground' : 'text-muted transition group-hover:text-foreground'}>
            Sign in
          </span>
        </Link>
        <Link
          href="/login?mode=register"
          className={`group inline-flex items-center justify-center rounded-[1rem] px-4 py-2.5 text-sm font-semibold transition ${
            registerSelected
              ? 'bg-foreground shadow-[0_16px_24px_rgba(15,26,40,0.16)]'
              : 'hover:bg-black/[0.02]'
          }`}
        >
          <span className={registerSelected ? 'text-sidebar-foreground' : 'text-muted transition group-hover:text-foreground'}>
            Create account
          </span>
        </Link>
      </div>

      {error ? (
        <div
          role="alert"
          className="flex items-start gap-3 rounded-[1.25rem] border border-red-200/80 bg-red-50/92 px-4 py-3.5 text-sm text-red-700"
        >
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none" className="mt-0.5 shrink-0" aria-hidden="true">
            <circle cx="8" cy="8" r="7" stroke="currentColor" strokeWidth="1.5"/>
            <path d="M8 5v3.5M8 10.5v.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
          </svg>
          {error}
        </div>
      ) : null}

      {registerSelected ? (
        <form action={registerConsoleOwner} className="grid gap-3">
          <AuthField
            autoComplete="organization"
            icon={<OfficeIcon />}
            label="Business name"
            name="businessName"
            type="text"
          />
          <AuthField autoComplete="email" icon={<MailIcon />} label="Owner email" name="email" type="email" />
          <AuthPasswordField
            autoComplete="new-password"
            hint="12+ characters, mixed case and numerics."
            label="Password"
            name="password"
          />
          <button
            type="submit"
            className="primary-button mt-1 w-full"
          >
            Create workspace
          </button>
          <p className="text-center text-xs text-muted">
            Already have an account?{' '}
            <Link href="/login" className="font-medium hover:underline">
              <span className="text-accent">Sign in</span>
            </Link>
          </p>
        </form>
      ) : (
        <form action={loginConsoleUser} className="grid gap-3">
          <AuthField autoComplete="email" icon={<MailIcon />} label="Email" name="email" type="email" />
          <AuthPasswordField autoComplete="current-password" label="Password" name="password" />
          <button
            type="submit"
            className="primary-button mt-1 w-full"
          >
            Sign in
          </button>
          <p className="text-center text-xs text-muted">
            Don&apos;t have an account?{' '}
            <Link href="/login?mode=register" className="font-medium hover:underline">
              <span className="text-accent">Create workspace</span>
            </Link>
          </p>
        </form>
      )}

      {!registerSelected ? (
        <div className="rounded-[1.25rem] border border-line bg-white/64 px-4 py-3 text-xs leading-5 text-muted">
          Tokens stay in an HttpOnly cookie. Browser JavaScript never receives direct bearer-token access.
        </div>
      ) : null}
    </div>
  );
}

function AuthField({
  autoComplete,
  hint,
  icon,
  label,
  name,
  type,
}: {
  autoComplete?: string;
  hint?: string;
  icon: React.ReactNode;
  label: string;
  name: string;
  type: string;
}) {
  return (
    <div className="grid gap-2">
      <label htmlFor={name} className="text-sm font-medium text-foreground">
        {label}
      </label>
      <div className="relative">
        <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-muted">
          {icon}
        </span>
        <input
          autoComplete={autoComplete}
          className="h-11 w-full rounded-[1rem] border border-line-strong bg-white/86 pl-11 pr-4 text-sm text-foreground shadow-[inset_0_1px_0_rgba(255,255,255,0.82)] outline-none transition placeholder:text-muted/60 focus:border-accent/40 focus:ring-4 focus:ring-accent-soft"
          id={name}
          name={name}
          required
          type={type}
        />
      </div>
      {hint ? <p className="text-[0.72rem] leading-4 text-muted">{hint}</p> : null}
    </div>
  );
}

function MailIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
      <rect x="2" y="3" width="12" height="10" rx="2" stroke="currentColor" strokeWidth="1.4"/>
      <path d="M3.5 5L8 8.5L12.5 5" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round"/>
    </svg>
  );
}

function OfficeIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
      <path d="M3 13V4.5L8.5 2v11" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round"/>
      <path d="M8.5 5H13v8H8.5" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round"/>
      <path d="M5.25 6.75h.01M5.25 9.5h.01" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round"/>
    </svg>
  );
}