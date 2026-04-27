'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { logoutConsoleUser } from '@/lib/console-auth-actions';

type NavItem = {
  href: string;
  label: string;
  note: string;
  icon: React.ReactNode;
  disabled?: boolean;
};

const primaryNav: NavItem[] = [
  {
    href: '/dashboard',
    label: 'Command Center',
    note: 'Readiness, activity, and rollout posture',
    icon: (
      <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden="true">
        <rect x="2" y="2" width="6" height="6" rx="1.5" stroke="currentColor" strokeWidth="1.5"/>
        <rect x="10" y="2" width="6" height="6" rx="1.5" stroke="currentColor" strokeWidth="1.5"/>
        <rect x="2" y="10" width="6" height="6" rx="1.5" stroke="currentColor" strokeWidth="1.5"/>
        <rect x="10" y="10" width="6" height="6" rx="1.5" stroke="currentColor" strokeWidth="1.5"/>
      </svg>
    ),
  },
  {
    href: '/agents',
    label: 'Agents',
    note: 'Voice contracts, languages, and runtime policy',
    icon: (
      <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden="true">
        <circle cx="9" cy="6" r="3" stroke="currentColor" strokeWidth="1.5"/>
        <path d="M3 15c0-3.314 2.686-6 6-6s6 2.686 6 6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
        <path d="M12 2l1.5 1.5M15 5.5h-1.5M12 9l1.5-1.5" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round"/>
      </svg>
    ),
  },
  {
    href: '/setup',
    label: 'Infrastructure',
    note: 'Secrets, API access, and production readiness',
    icon: (
      <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden="true">
        <path d="M9 2a7 7 0 1 0 0 14A7 7 0 0 0 9 2Z" stroke="currentColor" strokeWidth="1.5"/>
        <path d="M9 6v4l2.5 2.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
        <circle cx="14" cy="4" r="2.5" fill="currentColor" stroke="currentColor" strokeWidth="1"/>
      </svg>
    ),
  },
];

const secondaryNav: NavItem[] = [
  {
    href: '#',
    label: 'Knowledge Base',
    note: 'Document ingestion and retrieval verification',
    disabled: true,
    icon: (
      <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden="true">
        <path d="M3 4a1 1 0 0 1 1-1h4v12H4a1 1 0 0 1-1-1V4Z" stroke="currentColor" strokeWidth="1.5"/>
        <path d="M8 3h5a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H8V3Z" stroke="currentColor" strokeWidth="1.5"/>
        <path d="M5.5 7h1M5.5 9.5h1M10 7h3M10 9.5h3" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round"/>
      </svg>
    ),
  },
  {
    href: '#',
    label: 'Sessions',
    note: 'Live traffic, transcripts, and intervention view',
    disabled: true,
    icon: (
      <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden="true">
        <path d="M2 13l3.5-4L8 11.5 11 7l2.5 3L16 5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
      </svg>
    ),
  },
  {
    href: '#',
    label: 'Bookings',
    note: 'Pending holds, approvals, and expirations',
    disabled: true,
    icon: (
      <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden="true">
        <rect x="2" y="4" width="14" height="12" rx="2" stroke="currentColor" strokeWidth="1.5"/>
        <path d="M6 2v4M12 2v4M2 8h14" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
        <path d="M6 12h.01M9 12h.01M12 12h.01" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round"/>
      </svg>
    ),
  },
];

type ConsoleSidebarProps = {
  tenantName: string;
  email: string;
};

export function ConsoleSidebar({ tenantName, email }: ConsoleSidebarProps) {
  const pathname = usePathname();
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => {
    setMobileOpen(false);
  }, [pathname]);

  const isActive = (href: string) =>
    href === '/dashboard' ? pathname === '/dashboard' : pathname.startsWith(href);

  const renderNavItem = (item: NavItem) => {
    const active = !item.disabled && isActive(item.href);
    const content = (
      <>
        <span
          className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl transition ${
            item.disabled
              ? 'bg-white/[0.04] text-sidebar-muted/55'
              : active
                ? 'bg-accent text-white shadow-[0_18px_30px_rgba(20,115,230,0.30)]'
                : 'bg-white/[0.06] text-sidebar-foreground/92 group-hover:bg-white/[0.12]'
          }`}
        >
          {item.icon}
        </span>
        <span className="min-w-0 flex-1">
          <span
            className={`block text-sm font-semibold tracking-[-0.02em] ${
              item.disabled ? 'text-sidebar-foreground/72' : 'text-sidebar-foreground'
            }`}
          >
            {item.label}
          </span>
          <span className="mt-1 block text-xs leading-5 text-sidebar-muted">{item.note}</span>
        </span>
        {item.disabled ? (
          <span className="rounded-full border border-white/8 bg-white/[0.04] px-2 py-1 text-[0.62rem] font-medium uppercase tracking-[0.14em] text-sidebar-muted/78">
            Soon
          </span>
        ) : active ? (
          <span className="h-2.5 w-2.5 shrink-0 rounded-full bg-accent shadow-[0_0_0_6px_rgba(20,115,230,0.10)]" />
        ) : null}
      </>
    );

    const className = `group flex items-start gap-3 rounded-[1.35rem] border px-3.5 py-3.5 transition ${
      item.disabled
        ? 'cursor-not-allowed border-transparent bg-transparent'
        : active
          ? 'border-white/10 bg-white/[0.08] shadow-[inset_0_1px_0_rgba(255,255,255,0.04)]'
          : 'border-transparent hover:border-white/8 hover:bg-white/[0.05]'
    }`;

    if (item.disabled) {
      return (
        <span className={className}>
          {content}
        </span>
      );
    }

    return (
      <Link aria-current={active ? 'page' : undefined} href={item.href} className={className}>
        {content}
      </Link>
    );
  };

  const sidebar = (
    <aside className="surface-dark flex h-full flex-col overflow-hidden rounded-[2rem] px-4 py-4 text-sidebar-foreground">
      <div className="px-2">
        <div className="flex items-center justify-between gap-3 rounded-[1.4rem] border border-white/8 bg-white/[0.03] px-4 py-3.5">
          <div className="flex min-w-0 items-center gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-accent shadow-[0_18px_32px_rgba(20,115,230,0.30)]">
              <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
                <path d="M8 1.5a1 1 0 0 1 1 1v2a1 1 0 0 1-2 0v-2a1 1 0 0 1 1-1ZM4.22 3.22a1 1 0 0 1 1.414 0l1.414 1.414a1 1 0 0 1-1.414 1.414L4.22 4.634a1 1 0 0 1 0-1.414ZM2.5 7a1 1 0 0 1 1-1h2a1 1 0 1 1 0 2h-2a1 1 0 0 1-1-1ZM14.5 7a1 1 0 0 1-1 1h-2a1 1 0 1 1 0-2h2a1 1 0 0 1 1 1ZM8 11.5a1 1 0 0 1 1 1v2a1 1 0 1 1-2 0v-2a1 1 0 0 1 1-1Z" fill="white"/>
              </svg>
            </div>
            <div className="min-w-0">
              <p className="truncate text-base font-semibold tracking-[-0.03em]">AxonVoice</p>
              <p className="truncate text-xs text-sidebar-muted">Operator console</p>
            </div>
          </div>
          <span className="rounded-full border border-white/10 bg-white/[0.04] px-2.5 py-1 text-[0.62rem] font-medium uppercase tracking-[0.14em] text-sidebar-muted">
            Live
          </span>
        </div>

        <div className="mt-4 rounded-[1.6rem] border border-white/8 bg-white/[0.04] p-4">
          <p className="eyebrow text-sidebar-muted/74">Tenant</p>
          <p className="mt-2 truncate text-base font-semibold tracking-[-0.03em] text-sidebar-foreground">
            {tenantName}
          </p>
          <p className="mt-1 truncate text-sm text-sidebar-muted">{email}</p>
          <div className="mt-4 flex items-center gap-2 rounded-[1rem] bg-black/20 px-3 py-2 text-xs text-sidebar-muted">
            <svg width="14" height="14" viewBox="0 0 14 14" fill="none" aria-hidden="true">
              <rect x="2.5" y="6" width="9" height="5.5" rx="1.5" stroke="currentColor" strokeWidth="1.2"/>
              <path d="M4.5 6V4.75a2.5 2.5 0 0 1 5 0V6" stroke="currentColor" strokeWidth="1.2" strokeLinecap="round"/>
            </svg>
            Protected by a server-side operator session
          </div>
        </div>
      </div>

      <nav className="flex-1 overflow-y-auto px-2 py-5" aria-label="Primary navigation">
        <div>
          <p className="px-3 text-[0.65rem] font-medium uppercase tracking-[0.16em] text-sidebar-muted/72">
            Workspace
          </p>
          <ul className="mt-3 grid gap-1.5">
            {primaryNav.map((item) => (
              <li key={item.href}>{renderNavItem(item)}</li>
            ))}
          </ul>
        </div>

        <div className="mt-7">
          <p className="px-3 text-[0.65rem] font-medium uppercase tracking-[0.16em] text-sidebar-muted/72">
            Expansion
          </p>
          <ul className="mt-3 grid gap-1.5">
            {secondaryNav.map((item) => (
              <li key={item.label}>{renderNavItem(item)}</li>
            ))}
          </ul>
        </div>
      </nav>

      <div className="border-t border-white/8 px-2 pt-4">
        <form action={logoutConsoleUser}>
          <button
            type="submit"
            className="flex w-full items-center justify-between rounded-[1.2rem] border border-white/8 bg-white/[0.04] px-4 py-3 text-sm font-medium text-sidebar-foreground transition hover:bg-white/[0.08]"
          >
            <span className="flex items-center gap-3">
              <span className="flex h-10 w-10 items-center justify-center rounded-2xl bg-black/20 text-sidebar-muted">
                <svg width="18" height="18" viewBox="0 0 18 18" fill="none" aria-hidden="true">
                  <path d="M7 3H4a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h3M12 13l3-4-3-4M15 9H7" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/>
                </svg>
              </span>
              Sign out
            </span>
            <span className="text-sidebar-muted">Esc</span>
          </button>
        </form>
      </div>
    </aside>
  );

  return (
    <>
      <div className="hidden lg:fixed lg:inset-y-4 lg:left-4 lg:z-30 lg:flex lg:w-[17.5rem] lg:flex-col">
        {sidebar}
      </div>

      <header className="fixed inset-x-3 top-3 z-30 flex items-center justify-between rounded-[1.4rem] border border-line bg-[rgba(255,255,255,0.82)] px-4 py-3 shadow-[var(--shadow-md)] backdrop-blur-xl lg:hidden">
        <div className="flex items-center gap-2">
          <div className="flex h-9 w-9 items-center justify-center rounded-2xl bg-accent shadow-[0_14px_24px_rgba(20,115,230,0.24)]">
            <svg width="14" height="14" viewBox="0 0 16 16" fill="none" aria-hidden="true">
              <path d="M8 1.5a1 1 0 0 1 1 1v2a1 1 0 0 1-2 0v-2a1 1 0 0 1 1-1ZM14.5 7a1 1 0 0 1-1 1h-2a1 1 0 1 1 0-2h2a1 1 0 0 1 1 1ZM8 11.5a1 1 0 0 1 1 1v2a1 1 0 1 1-2 0v-2a1 1 0 0 1 1-1Z" fill="white"/>
            </svg>
          </div>
          <div>
            <p className="text-sm font-semibold tracking-[-0.02em] text-foreground">AxonVoice</p>
            <p className="text-[0.68rem] text-muted">{tenantName}</p>
          </div>
        </div>
        <button
          type="button"
          onClick={() => setMobileOpen(true)}
          className="rounded-xl border border-line bg-white/70 p-2 text-muted transition hover:text-foreground"
          aria-label="Open navigation"
        >
          <svg width="20" height="20" viewBox="0 0 20 20" fill="none" aria-hidden="true">
            <path d="M3 5h14M3 10h14M3 15h14" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round"/>
          </svg>
        </button>
      </header>

      {mobileOpen && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <div
            className="absolute inset-0 bg-[rgba(8,12,18,0.46)] backdrop-blur-sm"
            onClick={() => setMobileOpen(false)}
            aria-hidden="true"
          />
          <div className="absolute inset-y-3 left-3 w-[min(85vw,21rem)] shadow-2xl">
            {sidebar}
          </div>
        </div>
      )}
    </>
  );
}
