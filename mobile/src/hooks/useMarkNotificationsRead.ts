import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { notificationsApi } from '../api/client';
import type { MarkAllReadResponse, NotificationResponse } from '../types/api/notifications';
import { NOTIFICATIONS_QUERY_PREFIX } from './useNotifications';

/**
 * Mutation that marks one notification as read and refreshes the inbox, so the unread counter drops.
 * @returns The TanStack Query mutation; its variable is the notification identifier.
 */
export function useMarkNotificationRead(): UseMutationResult<NotificationResponse, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (notificationId: string) => notificationsApi.markRead(notificationId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_QUERY_PREFIX }),
  });
}

/**
 * Mutation that marks the whole inbox as read and refreshes it.
 * @returns The TanStack Query mutation.
 */
export function useMarkAllNotificationsRead(): UseMutationResult<MarkAllReadResponse, Error, void> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => notificationsApi.markAllRead(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_QUERY_PREFIX }),
  });
}
