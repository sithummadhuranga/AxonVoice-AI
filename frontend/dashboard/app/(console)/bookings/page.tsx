import { ActionSubmitButton } from '@/components/action-submit-button';
import { MutationBanner } from '@/components/mutation-banner';
import Link from 'next/link';
import {
  getAgentBookings,
  getAgents,
  getBusinessHours,
  getClosedDates,
  getErrorMessage,
  type AgentSummary,
  type BusinessHoursEntry,
  type ClosedDateEntry,
  type ConfirmedBookingSummary,
  type PendingBookingSummary,
} from '@/lib/api';
import { getLanguageLabel } from '@/lib/agent-form';
import { confirmPendingBookingAction, expirePendingBookingAction, setBusinessHoursAction } from '@/lib/booking-actions';
import { formatConsoleDate, formatConsoleDateTime, formatTimeRemaining, maskContactValue } from '@/lib/console-format';
import { requireConsoleSession } from '@/lib/console-session';

type BookingsPageProps = {
  searchParams: Promise<{
    agentId?: string;
    error?: string;
    message?: string;
  }>;
};

const weekdayLabels = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];

export default async function BookingsPage({ searchParams }: BookingsPageProps) {
  await requireConsoleSession();

  const [{ agentId: requestedAgentId, error, message }, agents] = await Promise.all([
    searchParams,
    getAgents().catch(() => []),
  ]);

  if (agents.length === 0) {
    return <EmptyAgentState title="Bookings" description="Create an agent before managing holds, approvals, and expirations." />;
  }

  const selectedAgent = resolveSelectedAgent(agents, requestedAgentId);

  let pendingBookings: PendingBookingSummary[] = [];
  let confirmedBookings: ConfirmedBookingSummary[] = [];
  let businessHours: BusinessHoursEntry[] = [];
  let closedDates: ClosedDateEntry[] = [];
  let loadError: string | null = null;

  try {
    const [bookings, hours, dates] = await Promise.all([
      getAgentBookings(selectedAgent.id),
      getBusinessHours(selectedAgent.id),
      getClosedDates(selectedAgent.id),
    ]);

    pendingBookings = bookings.pendingBookings;
    confirmedBookings = bookings.confirmedBookings;
    businessHours = hours;
    closedDates = dates;
  } catch (cause) {
    loadError = getErrorMessage(cause);
  }

  const livePendingCount = pendingBookings.filter((booking) => booking.status === 'pending').length;
  const expiredCount = pendingBookings.filter((booking) => booking.status === 'expired').length;

  return (
    <div className="grid gap-6">
      <section className="surface-card-strong relative overflow-hidden p-6 lg:p-8">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{
            background:
              'radial-gradient(circle at 86% 14%, rgba(20,115,230,0.12) 0%, transparent 24%), radial-gradient(circle at 8% 84%, rgba(255,122,69,0.10) 0%, transparent 22%)',
          }}
        />
        <div className="relative grid gap-6 xl:grid-cols-[minmax(0,1fr)_18rem] xl:items-start">
          <div>
            <p className="eyebrow text-accent">Bookings</p>
            <h1 className="page-title mt-4 max-w-3xl text-foreground">
              Confirm live holds before they expire and keep the operator side of reservations tight.
            </h1>
            <p className="page-subtitle mt-5">
              This workflow shows pending holds created by the relay, confirmed bookings already locked in, and the schedule context the availability query is protecting.
            </p>
            <div className="mt-7 flex flex-wrap gap-3">
              <Link href="/dashboard" className="secondary-button">
                Return to overview
              </Link>
              <Link href={buildBookingsUrl(selectedAgent.id)} className="secondary-button">
                Refresh bookings
              </Link>
            </div>
          </div>

          <div className="grid gap-3">
            <SummaryCard label="Pending holds" value={livePendingCount.toString()} />
            <SummaryCard label="Expired holds" value={expiredCount.toString()} />
            <SummaryCard label="Confirmed bookings" value={confirmedBookings.length.toString()} />
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
              Booking queue scope
            </h2>
          </div>
          <div className="flex flex-wrap gap-2">
            {agents.map((agent) => (
              <FilterChip key={agent.id} href={buildBookingsUrl(agent.id)} active={agent.id === selectedAgent.id}>
                {agent.displayName}
              </FilterChip>
            ))}
          </div>
        </div>
      </section>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="grid gap-6">
          <section className="surface-card-strong overflow-hidden p-5 lg:p-6">
            <div className="border-b border-line pb-4">
              <p className="eyebrow text-muted">Pending holds</p>
              <h2 className="mt-2 text-lg font-semibold tracking-[-0.03em] text-foreground">
                Holds waiting for operator action
              </h2>
            </div>

            {pendingBookings.length === 0 ? (
              <EmptyState description="No pending or expired holds exist for this agent right now." />
            ) : (
              <div className="mt-4 grid gap-4">
                {pendingBookings.map((booking) => (
                  <PendingBookingCard key={booking.id} agentId={selectedAgent.id} booking={booking} />
                ))}
              </div>
            )}
          </section>

          <section className="surface-card-strong overflow-hidden p-5 lg:p-6">
            <div className="border-b border-line pb-4">
              <p className="eyebrow text-muted">Confirmed bookings</p>
              <h2 className="mt-2 text-lg font-semibold tracking-[-0.03em] text-foreground">
                Reservations already promoted from a live hold
              </h2>
            </div>

            {confirmedBookings.length === 0 ? (
              <EmptyState description="No confirmed bookings have been promoted for this agent yet." />
            ) : (
              <div className="mt-4 grid gap-4">
                {confirmedBookings.map((booking) => (
                  <ConfirmedBookingCard key={booking.id} booking={booking} />
                ))}
              </div>
            )}
          </section>
        </div>

        <aside className="grid gap-4 self-start xl:sticky xl:top-4">
          <section className="surface-card p-5">
            <p className="eyebrow text-muted">Schedule setup</p>
            <h2 className="mt-2 text-lg font-semibold tracking-[-0.03em] text-foreground">
              Availability source of truth
            </h2>
            <p className="mt-2 text-sm leading-6 text-muted">
              Reservation and appointment tools stay inactive until at least one active business-hours row is saved for this agent.
            </p>

            <form action={setBusinessHoursAction} className="mt-4 grid gap-3">
              <input type="hidden" name="agentId" value={selectedAgent.id} />
              {weekdayLabels.map((label, dayOfWeek) => {
                const configuredSlot = businessHours.find((slot) => slot.dayOfWeek === dayOfWeek);
                return (
                  <ScheduleEditorRow
                    key={label}
                    dayLabel={label}
                    dayOfWeek={dayOfWeek}
                    slot={configuredSlot}
                  />
                );
              })}

              <ActionSubmitButton
                className="primary-button h-12 disabled:cursor-wait disabled:opacity-80"
                idleLabel="Save schedule"
                pendingLabel="Saving schedule..."
              />
            </form>
          </section>

          <section className="surface-card p-5">
            <p className="eyebrow text-muted">Business hours</p>
            <div className="mt-4 grid gap-3">
              {businessHours.length === 0 ? (
                <p className="text-sm text-muted">No business hours have been configured for this agent yet.</p>
              ) : (
                businessHours.map((slot) => (
                  <InfoCard
                    key={slot.id}
                    label={weekdayLabels[slot.dayOfWeek] ?? `Day ${slot.dayOfWeek}`}
                    value={`${formatTimeValue(slot.openTime)} - ${formatTimeValue(slot.closeTime)} · ${slot.slotDurationMinutes} min slots · capacity ${slot.maxCapacityPerSlot}`}
                  />
                ))
              )}
            </div>
          </section>

          <section className="surface-card p-5">
            <p className="eyebrow text-muted">Closed dates</p>
            <div className="mt-4 grid gap-3">
              {closedDates.length === 0 ? (
                <p className="text-sm text-muted">No closed dates are blocking capacity right now.</p>
              ) : (
                closedDates.map((closedDate) => (
                  <InfoCard
                    key={closedDate.id}
                    label={formatConsoleDate(closedDate.date)}
                    value={closedDate.reason ?? 'Closed'}
                  />
                ))
              )}
            </div>
          </section>
        </aside>
      </div>
    </div>
  );
}

function PendingBookingCard({ agentId, booking }: { agentId: string; booking: PendingBookingSummary }) {
  const isPending = booking.status === 'pending';

  return (
    <article className="rounded-[1.5rem] border border-line bg-white/82 p-5 shadow-[var(--shadow-sm)]">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="text-base font-semibold tracking-[-0.02em] text-foreground">{booking.customerName}</h3>
            <StatusChip status={booking.status} />
          </div>
          <p className="mt-2 text-sm text-muted">
            {booking.partySize} guests · {formatConsoleDateTime(booking.requestedDatetime)} · {getLanguageLabel(booking.customerLanguage)}
          </p>
          <p className="mt-2 text-sm text-muted">
            {maskContactValue(booking.customerPhone)} · code {booking.confirmationCode}
          </p>
          {booking.specialRequests ? (
            <p className="mt-2 text-sm leading-6 text-foreground">{booking.specialRequests}</p>
          ) : null}
        </div>

        <div className="rounded-[1rem] border border-line bg-[rgba(245,247,250,0.9)] px-4 py-3 text-sm text-muted">
          <p>Created {formatConsoleDateTime(booking.createdAt)}</p>
          <p className="mt-1">{formatTimeRemaining(booking.expiresAt)}</p>
        </div>
      </div>

      {isPending ? (
        <div className="mt-4 grid gap-3 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-end">
          <form action={confirmPendingBookingAction} className="grid gap-3 md:grid-cols-[minmax(0,1fr)_auto]">
            <input type="hidden" name="agentId" value={agentId} />
            <input type="hidden" name="bookingId" value={booking.id} />
            <label className="grid gap-2">
              <span className="text-xs font-medium uppercase tracking-[0.12em] text-muted">Operator notes</span>
              <input
                name="internalNotes"
                className="h-12 rounded-[1rem] border border-line-strong bg-white/86 px-4 text-sm text-foreground shadow-[inset_0_1px_0_rgba(255,255,255,0.82)] outline-none transition placeholder:text-muted/60 focus:border-accent/40 focus:ring-4 focus:ring-accent-soft"
                placeholder="Seat near the entrance"
              />
            </label>
            <ActionSubmitButton
              className="primary-button h-12 whitespace-nowrap disabled:cursor-wait disabled:opacity-80"
              idleLabel="Confirm booking"
              pendingLabel="Confirming..."
            />
          </form>

          <form action={expirePendingBookingAction}>
            <input type="hidden" name="agentId" value={agentId} />
            <input type="hidden" name="bookingId" value={booking.id} />
            <ActionSubmitButton
              className="secondary-button h-12 whitespace-nowrap disabled:cursor-wait disabled:opacity-80"
              idleLabel="Expire hold"
              pendingLabel="Expiring..."
            />
          </form>
        </div>
      ) : null}
    </article>
  );
}

function ConfirmedBookingCard({ booking }: { booking: ConfirmedBookingSummary }) {
  return (
    <article className="rounded-[1.5rem] border border-line bg-white/82 p-5 shadow-[var(--shadow-sm)]">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="text-base font-semibold tracking-[-0.02em] text-foreground">{booking.customerName}</h3>
            <StatusChip status={booking.status} />
          </div>
          <p className="mt-2 text-sm text-muted">
            {booking.partySize} guests · {formatConsoleDateTime(booking.bookingDatetime)} · {getLanguageLabel(booking.customerLanguage)}
          </p>
          <p className="mt-2 text-sm text-muted">
            {maskContactValue(booking.customerPhone)} · code {booking.confirmationCode}
          </p>
          {booking.specialRequests ? (
            <p className="mt-2 text-sm leading-6 text-foreground">{booking.specialRequests}</p>
          ) : null}
          {booking.internalNotes ? (
            <p className="mt-2 text-sm leading-6 text-muted">Operator notes: {booking.internalNotes}</p>
          ) : null}
        </div>

        <div className="rounded-[1rem] border border-line bg-[rgba(245,247,250,0.9)] px-4 py-3 text-sm text-muted">
          <p>Confirmed {formatConsoleDateTime(booking.confirmedAt)}</p>
          <p className="mt-1">By {booking.confirmedBy ?? 'operator'}</p>
        </div>
      </div>
    </article>
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

function StatusChip({ status }: { status: string }) {
  const className = status === 'pending'
    ? 'bg-amber-50 text-amber-700'
    : status === 'confirmed'
      ? 'bg-emerald-50 text-emerald-700'
      : 'bg-black/[0.05] text-muted';

  return (
    <span className={`rounded-full px-3 py-1.5 text-[0.68rem] font-medium uppercase tracking-[0.12em] ${className}`}>
      {status}
    </span>
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

function EmptyState({ description }: { description: string }) {
  return <div className="px-4 py-12 text-sm text-muted">{description}</div>;
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

function buildBookingsUrl(agentId: string): string {
  return `/bookings?${new URLSearchParams({ agentId }).toString()}`;
}

function ScheduleEditorRow({
  dayLabel,
  dayOfWeek,
  slot,
}: {
  dayLabel: string;
  dayOfWeek: number;
  slot: BusinessHoursEntry | undefined;
}) {
  return (
    <div className="rounded-[1.15rem] border border-line bg-white/82 px-4 py-4">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm font-semibold text-foreground">{dayLabel}</p>
        <label className="flex items-center gap-2 text-xs font-medium uppercase tracking-[0.12em] text-muted">
          <input
            className="h-4 w-4 accent-[var(--color-accent)]"
            defaultChecked={slot?.isActive ?? false}
            name={`dayEnabled_${dayOfWeek}`}
            type="checkbox"
          />
          Enabled
        </label>
      </div>

      <div className="mt-3 grid gap-3 sm:grid-cols-2">
        <label className="grid gap-2 text-xs font-medium uppercase tracking-[0.12em] text-muted">
          Open
          <input
            className="h-11 rounded-[1rem] border border-line-strong bg-white/86 px-3 text-sm text-foreground outline-none transition focus:border-accent/40 focus:ring-4 focus:ring-accent-soft"
            defaultValue={toTimeInputValue(slot?.openTime, '09:00')}
            name={`openTime_${dayOfWeek}`}
            type="time"
          />
        </label>
        <label className="grid gap-2 text-xs font-medium uppercase tracking-[0.12em] text-muted">
          Close
          <input
            className="h-11 rounded-[1rem] border border-line-strong bg-white/86 px-3 text-sm text-foreground outline-none transition focus:border-accent/40 focus:ring-4 focus:ring-accent-soft"
            defaultValue={toTimeInputValue(slot?.closeTime, '17:00')}
            name={`closeTime_${dayOfWeek}`}
            type="time"
          />
        </label>
      </div>

      <div className="mt-3 grid gap-3 sm:grid-cols-2">
        <label className="grid gap-2 text-xs font-medium uppercase tracking-[0.12em] text-muted">
          Slot duration
          <input
            className="h-11 rounded-[1rem] border border-line-strong bg-white/86 px-3 text-sm text-foreground outline-none transition focus:border-accent/40 focus:ring-4 focus:ring-accent-soft"
            defaultValue={String(slot?.slotDurationMinutes ?? 60)}
            min={1}
            name={`slotDurationMinutes_${dayOfWeek}`}
            type="number"
          />
        </label>
        <label className="grid gap-2 text-xs font-medium uppercase tracking-[0.12em] text-muted">
          Slot capacity
          <input
            className="h-11 rounded-[1rem] border border-line-strong bg-white/86 px-3 text-sm text-foreground outline-none transition focus:border-accent/40 focus:ring-4 focus:ring-accent-soft"
            defaultValue={String(slot?.maxCapacityPerSlot ?? 10)}
            min={1}
            name={`maxCapacityPerSlot_${dayOfWeek}`}
            type="number"
          />
        </label>
      </div>
    </div>
  );
}

function toTimeInputValue(value: string | undefined, fallback: string): string {
  if (!value || value.length < 5) {
    return fallback;
  }

  return value.slice(0, 5);
}

function formatTimeValue(value: string): string {
  return value.length >= 5 ? value.slice(0, 5) : value;
}