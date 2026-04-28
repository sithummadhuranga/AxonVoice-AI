type MutationBannerProps = {
  children: React.ReactNode;
  tone: 'error' | 'success' | 'warning';
};

export function MutationBanner({ children, tone }: MutationBannerProps) {
  const styles = tone === 'error'
    ? {
        container: 'border-red-200/80 bg-red-50/92 text-red-700',
        icon: 'text-red-500',
      }
    : tone === 'success'
      ? {
          container: 'border-emerald-200/80 bg-emerald-50/92 text-emerald-700',
          icon: 'text-emerald-500',
        }
      : {
          container: 'border-amber-200/80 bg-amber-50/92 text-amber-700',
          icon: 'text-amber-500',
        };

  return (
    <section
      aria-live="polite"
      className={`flex items-start gap-3 rounded-[1.35rem] border px-4 py-3.5 text-sm shadow-[var(--shadow-sm)] ${styles.container}`}
      role="status"
    >
      <span className={`mt-0.5 shrink-0 ${styles.icon}`} aria-hidden="true">
        {tone === 'success' ? (
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
            <circle cx="8" cy="8" r="6.5" stroke="currentColor" strokeWidth="1.5" />
            <path d="M5 8.25L7 10.25L11 6.25" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        ) : tone === 'warning' ? (
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
            <path d="M8 1.5L14.5 13H1.5L8 1.5Z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round" />
            <path d="M8 6V9.5M8 11V11.5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
          </svg>
        ) : (
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
            <circle cx="8" cy="8" r="6.5" stroke="currentColor" strokeWidth="1.5" />
            <path d="M8 5V8.5M8 10.5V11" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
          </svg>
        )}
      </span>
      <div>{children}</div>
    </section>
  );
}