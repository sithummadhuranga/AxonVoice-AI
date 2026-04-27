import { redirect } from 'next/navigation';
import { ConsoleShellHeader } from '@/components/console-shell-header';
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

      <div className="relative min-h-screen pt-20 sm:pt-20 lg:pl-[21rem] lg:pt-0 xl:pl-[22.25rem] 2xl:pl-[23.75rem]">
        <ConsoleShellHeader tenantName={session.tenantName} />
        <main className="px-3 pb-4 pt-5 sm:px-4 lg:px-5 lg:pb-8 lg:pt-6 2xl:px-6">
          <div className="mx-auto flex max-w-[84rem] flex-col gap-6 2xl:max-w-[90rem]">
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}
