'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

type RouteMeta = {
  breadcrumbs: { label: string; href?: string }[];
  label: string;
};

export function ConsoleShellHeader({ tenantName }: { tenantName: string }) {
  const pathname = usePathname();
  const meta = getRouteMeta(pathname);

  return (
    <header className="sticky top-0 z-20 hidden border-b border-line bg-[rgba(248,249,252,0.88)] backdrop-blur-xl lg:block">
      <div className="flex h-14 items-center justify-between gap-4 px-5 2xl:px-6">

        {/* Breadcrumb */}
        <nav aria-label="Breadcrumb" className="flex min-w-0 items-center gap-1">
          <Link
            href="/dashboard"
            className="group flex shrink-0 items-center gap-2 rounded-xl px-2 py-1.5 transition hover:bg-black/[0.04]"
            aria-label="Go to console home"
          >
            <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-lg bg-accent shadow-[0_6px_14px_rgba(20,115,230,0.28)]">
              <svg width="10" height="10" viewBox="0 0 16 16" fill="none" aria-hidden="true">
                <path
                  d="M8 1.5a1 1 0 0 1 1 1v2a1 1 0 0 1-2 0v-2a1 1 0 0 1 1-1ZM4.22 3.22a1 1 0 0 1 1.414 0l1.414 1.414a1 1 0 0 1-1.414 1.414L4.22 4.634a1 1 0 0 1 0-1.414ZM2.5 7a1 1 0 0 1 1-1h2a1 1 0 1 1 0 2h-2a1 1 0 0 1-1-1ZM14.5 7a1 1 0 0 1-1 1h-2a1 1 0 1 1 0-2h2a1 1 0 0 1 1 1ZM8 11.5a1 1 0 0 1 1 1v2a1 1 0 1 1-2 0v-2a1 1 0 0 1 1-1Z"
                  fill="white"
                />
              </svg>
            </span>
            <span className="text-sm font-semibold tracking-[-0.02em] text-foreground transition group-hover:text-accent">
              AxonVoice
            </span>
          </Link>

          {meta.breadcrumbs.map((crumb, index) => (
            <span key={`${crumb.label}-${index}`} className="flex min-w-0 items-center gap-1">
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none" aria-hidden="true" className="shrink-0 text-muted/36">
                <path d="M5 3l4 4-4 4" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              {crumb.href ? (
                <Link
                  href={crumb.href}
                  className="truncate rounded-lg px-2 py-1.5 text-sm font-medium text-muted transition hover:bg-black/[0.04] hover:text-foreground"
                >
                  {crumb.label}
                </Link>
              ) : (
                <span className="truncate rounded-lg px-2 py-1.5 text-sm font-semibold text-foreground">
                  {crumb.label}
                </span>
              )}
            </span>
          ))}
        </nav>

        {/* Right — workspace chip */}
        <div className="flex shrink-0 items-center gap-3">
          <span className="hidden truncate text-xs font-medium text-muted/72 xl:block xl:max-w-[16rem]">
            {meta.label}
          </span>
          <span className="hidden h-4 w-px bg-line xl:block" aria-hidden="true" />
          <div className="flex items-center gap-2 rounded-[0.9rem] border border-line bg-white/82 px-3 py-1.5 shadow-[var(--shadow-sm)]">
            <svg width="12" height="12" viewBox="0 0 14 14" fill="none" aria-hidden="true" className="shrink-0 text-accent">
              <rect x="1.5" y="5" width="11" height="7.5" rx="1.8" stroke="currentColor" strokeWidth="1.3" />
              <path d="M4.5 5V4a2.5 2.5 0 0 1 5 0v1" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" />
            </svg>
            <span className="max-w-[12rem] truncate text-xs font-semibold tracking-[-0.01em] text-foreground">
              {tenantName}
            </span>
          </div>
        </div>
      </div>
    </header>
  );
}

function getRouteMeta(pathname: string): RouteMeta {
  if (pathname === '/agents/new') {
    return {
      breadcrumbs: [
        { label: 'Agents', href: '/agents' },
        { label: 'New contract' },
      ],
      label: 'New agent contract',
    };
  }

  if (pathname.startsWith('/agents/') && pathname !== '/agents/new') {
    return {
      breadcrumbs: [
        { label: 'Agents', href: '/agents' },
        { label: 'Editor' },
      ],
      label: 'Agent editor',
    };
  }

  if (pathname === '/agents') {
    return {
      breadcrumbs: [{ label: 'Agents' }],
      label: 'Agent inventory',
    };
  }

  if (pathname === '/setup') {
    return {
      breadcrumbs: [{ label: 'Infrastructure' }],
      label: 'Credential management',
    };
  }

  return {
    breadcrumbs: [{ label: 'Command center' }],
    label: 'Operator overview',
  };
}