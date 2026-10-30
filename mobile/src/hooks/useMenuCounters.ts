import type { MenuCounters } from '../types/hooks/useMenuCounters.types';
import { useNotifications } from './useNotifications';
import { useRefunds } from './useRefunds';

/**
 * Counts what is waiting for the photographer, from the same queries the inbox and the list of refunds use, so the numbers stay
 * current while the app is open.
 * @returns The unread notices, the refunds still owed and whether either is waiting.
 */
export function useMenuCounters(): MenuCounters {
  const unreadCount = useNotifications().data?.unreadCount ?? 0;
  const pendingRefunds = useRefunds().data?.pendingCount ?? 0;

  return { unreadCount, pendingRefunds, needsAttention: unreadCount > 0 || pendingRefunds > 0 };
}
