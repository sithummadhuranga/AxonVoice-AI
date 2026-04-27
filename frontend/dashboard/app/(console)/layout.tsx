import { redirect } from 'next/navigation';
import { ConsoleSidebar } from '@/components/console-sidebar';
import { requireConsoleSession } from '@/lib/console-session';

export default async function ConsoleLayout({ children }: { children: React.ReactNode }) {
  const session = await requireConsoleSession().catch(() => null);
  if (!session) {
    redirect('/login');
  }

  return (
    <div className="min-h-screen bg-background">
      <ConsoleSidebar tenantName={session.tenantName} email={session.email} />

      <div className="relative min-h-screen px-3 pb-4 pt-20 lg:px-4 lg:pb-6 lg:pl-[19rem] lg:pt-4">
        <main className="mx-auto max-w-[77rem]">
          {children}
        </main>
      </div>
    </div>
  );
}
