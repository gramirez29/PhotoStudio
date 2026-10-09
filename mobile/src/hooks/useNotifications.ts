import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { notificationsApi } from '../api/client';
import { useSessionStore } from '../session/sessionStore';
import type { NotificationListResponse } from '../types/api/notifications';

/** Prefix shared by the cache keys of the inbox, to refresh it from the mutations. */
export const NOTIFICATIONS_QUERY_PREFIX = ['notifications'] as const;

/** How often the inbox is reloaded while the app is open, in milliseconds. New notices arrive from a background job. */
export const NOTIFICATIONS_REFETCH_MS = 60_000;

/**
 * Builds the cache key of a photographer's inbox. The photographer is part of the key so that the inbox of one account is
 * never shown to another that signs in on the same device.
 * @param photographerId Photographer (tenant) identifier.
 * @returns The query key.
 */
export function notificationsQueryKey(photographerId: string): readonly ['notifications', string] {
  return ['notifications', photographerId] as const;
}

/**
 * Loads the inbox of the signed-in photographer and reloads it every minute, so a notice delivered by the background job
 * shows up without pulling to refresh.
 * @returns The TanStack Query result; the query is disabled while there is no session.
 */
export function useNotifications(): UseQueryResult<NotificationListResponse, Error> {
  const photographerId = useSessionStore((state) => state.session?.photographerId ?? null);

  return useQuery({
    queryKey: notificationsQueryKey(photographerId ?? ''),
    queryFn: ({ signal }) => notificationsApi.listNotifications(signal),
    enabled: photographerId !== null,
    refetchInterval: NOTIFICATIONS_REFETCH_MS,
  });
}
