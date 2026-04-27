'use client';

import { useState } from 'react';

export function AuthPasswordField({
  autoComplete,
  hint,
  label,
  name,
}: {
  autoComplete?: string;
  hint?: string;
  label: string;
  name: string;
}) {
  const [visible, setVisible] = useState(false);

  return (
    <div className="grid gap-2">
      <label htmlFor={name} className="text-sm font-medium text-foreground">
        {label}
      </label>
      <div className="relative">
        <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-muted">
          <LockIcon />
        </span>
        <input
          autoComplete={autoComplete}
          className="h-11 w-full rounded-[1rem] border border-line-strong bg-white/86 pl-11 pr-11 text-sm text-foreground shadow-[inset_0_1px_0_rgba(255,255,255,0.82)] outline-none transition placeholder:text-muted/60 focus:border-accent/40 focus:ring-4 focus:ring-accent-soft"
          id={name}
          name={name}
          required
          type={visible ? 'text' : 'password'}
        />
        <button
          type="button"
          onClick={() => setVisible((v) => !v)}
          className="absolute inset-y-0 right-3 flex items-center text-muted transition hover:text-foreground"
          aria-label={visible ? 'Hide password' : 'Show password'}
          tabIndex={-1}
        >
          {visible ? <EyeOffIcon /> : <EyeIcon />}
        </button>
      </div>
      {hint ? <p className="text-[0.72rem] leading-4 text-muted">{hint}</p> : null}
    </div>
  );
}

function LockIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
      <rect x="3" y="7" width="10" height="6" rx="2" stroke="currentColor" strokeWidth="1.4" />
      <path d="M5.5 7V5.75a2.5 2.5 0 1 1 5 0V7" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
    </svg>
  );
}

function EyeIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
      <path d="M1.5 8C1.5 8 4 3 8 3s6.5 5 6.5 5-2.5 5-6.5 5S1.5 8 1.5 8Z" stroke="currentColor" strokeWidth="1.4" strokeLinejoin="round" />
      <circle cx="8" cy="8" r="2" stroke="currentColor" strokeWidth="1.4" />
    </svg>
  );
}

function EyeOffIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
      <path d="M2 2l12 12M6.5 6.6A2 2 0 0 0 9.4 9.5" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
      <path d="M4.2 4.3C2.8 5.3 1.5 7 1.5 8c0 0 2.5 5 6.5 5 1.4 0 2.7-.5 3.8-1.3" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M12.5 10.5C13.5 9.5 14.5 8 14.5 8c0 0-2.5-5-6.5-5-.7 0-1.4.1-2 .3" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
