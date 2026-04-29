import { redirect } from 'next/navigation';
import { getConsoleSession } from '@/lib/console-session';

export default async function Home() {
  const session = await getConsoleSession();
  redirect(session ? '/dashboard' : '/login');
}
