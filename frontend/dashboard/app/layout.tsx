import type { Metadata } from 'next';
import Link from 'next/link';
import { IBM_Plex_Mono, Manrope } from 'next/font/google';
import { logoutConsoleUser } from '@/lib/console-auth-actions';
import { getConsoleSession } from '@/lib/console-session';
import './globals.css';

const manrope = Manrope({
  variable: '--font-manrope',
  subsets: ['latin'],
});

const ibmPlexMono = IBM_Plex_Mono({
  variable: '--font-ibm-plex-mono',
  subsets: ['latin'],
  weight: ['400', '500'],
});

const navigation = [
  {
    href: '/dashboard',
    label: 'Overview',
    description: 'Launch readiness and operating posture',
  },
  {
    href: '/setup',
    label: 'Setup',
    description: 'Gemini access and onboarding progress',
  },
  {
    href: '/agents',
    label: 'Agents',
    description: 'Voice personas, language coverage, and session limits',
  },
];

const secondarySections = [
  'Knowledge base',
  'Sessions',
  'Bookings',
];

export const metadata: Metadata = {
  title: 'AxonVoice Console',
  description: 'Tenant administration for multilingual voice agents.',
};

export default async function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? process.env.AXONVOICE_API_URL ?? null;
  const session = await getConsoleSession();

  return (
    <html lang="en" className={`${manrope.variable} ${ibmPlexMono.variable}`}>
      <body>
        <div className="min-h-screen lg:grid lg:grid-cols-[320px_1fr]">
          <aside className="border-b border-sidebar-muted/20 bg-sidebar px-6 py-8 text-sidebar-foreground lg:border-b-0 lg:border-r lg:px-8 lg:py-10">
            <div className="max-w-xs">
              <p className="font-mono text-[0.72rem] uppercase tracking-[0.3em] text-sidebar-muted">
                AxonVoice
              </p>
              <h1 className="mt-3 text-4xl font-semibold tracking-[-0.07em] text-sidebar-foreground">
                Tenant console
              </h1>
              <p className="mt-4 text-sm leading-6 text-sidebar-muted">
                Configure business-ready voice agents without exposing Gemini credentials or session relay internals.
              </p>
            </div>

            <nav className="mt-10 flex flex-col gap-3">
              {navigation.map((item) => (
                <Link
                  key={item.href}
                  href={item.href}
                  className="rounded-3xl border border-sidebar-muted/15 bg-white/6 px-4 py-4 transition hover:bg-white/10"
                >
                  <p className="text-base font-semibold tracking-[-0.03em] text-sidebar-foreground">
                    {item.label}
                  </p>
                  <p className="mt-1 text-sm text-sidebar-muted">{item.description}</p>
                </Link>
              ))}
            </nav>

            <section className="mt-10 rounded-3xl border border-sidebar-muted/15 bg-white/6 p-5">
              <p className="font-mono text-[0.72rem] uppercase tracking-[0.24em] text-sidebar-muted">
                API origin
              </p>
              <p className="mt-3 break-all text-sm leading-6 text-sidebar-foreground">
                {apiUrl ?? 'Set NEXT_PUBLIC_API_URL or AXONVOICE_API_URL to connect the dashboard.'}
              </p>
            </section>

            <section className="mt-6 grid gap-3">
              {secondarySections.map((label) => (
                <div
                  key={label}
                  className="rounded-2xl border border-sidebar-muted/12 px-4 py-3 text-sm text-sidebar-muted"
                >
                  <div className="flex items-center justify-between gap-4">
                    <span>{label}</span>
                    <span className="rounded-full border border-sidebar-muted/20 px-2 py-1 text-[0.68rem] uppercase tracking-[0.16em]">
                      Soon
                    </span>
                  </div>
                </div>
              ))}
            </section>
          </aside>

          <div className="min-h-screen">
            <header className="border-b border-line/70 bg-panel/75 px-6 py-5 backdrop-blur lg:px-10">
              <div className="mx-auto flex max-w-6xl items-center justify-between gap-4">
                <div>
                  <p className="font-mono text-[0.72rem] uppercase tracking-[0.28em] text-accent">
                    Operations
                  </p>
                  <p className="mt-2 text-sm text-muted">
                    {session
                      ? `Authenticated for ${session.tenantName}. Dashboard requests stay server-side and tenant-scoped.`
                      : 'Sign in to reach tenant-scoped gateway routes from the dashboard server.'}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  {session ? (
                    <>
                      <div className="rounded-full border border-line bg-white/70 px-4 py-2 text-sm text-foreground shadow-[0_10px_30px_rgba(49,34,21,0.08)]">
                        {session.email}
                      </div>
                      <form action={logoutConsoleUser}>
                        <button
                          type="submit"
                          className="rounded-full border border-line bg-sidebar px-4 py-2 text-sm text-sidebar-foreground transition hover:bg-black"
                        >
                          Sign out
                        </button>
                      </form>
                    </>
                  ) : (
                    <Link
                      href="/login"
                      className="rounded-full border border-line bg-white/70 px-4 py-2 text-sm text-foreground shadow-[0_10px_30px_rgba(49,34,21,0.08)] transition hover:bg-white"
                    >
                      Sign in
                    </Link>
                  )}
                </div>
              </div>
            </header>

            <main className="mx-auto max-w-6xl px-6 py-8 lg:px-10 lg:py-10">
              {children}
            </main>
          </div>
        </div>
      </body>
    </html>
  );
}
