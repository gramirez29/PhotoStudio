import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { openBooking } from '../navigation/appNavigation';
import { registerPushDevice, subscribeToPushEvents } from '../push/pushManager';
import { useSessionStore } from '../session/sessionStore';
import { NOTIFICATIONS_QUERY_PREFIX } from './useNotifications';

/**
 * Connects the app to push notifications while the photographer is signed in: registers the phone with the backend (each time
 * the app opens, which also refreshes a token that changed), reloads the inbox when a notification arrives while the app is
 * open, and opens the booking when a notification is tapped. Does nothing while signed out.
 */
export function usePushRegistration(): void {
  const signedIn = useSessionStore((state) => state.status === 'signedIn');
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!signedIn) {
      return undefined;
    }

    void registerPushDevice();

    return subscribeToPushEvents({
      onReceived: () => void queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_QUERY_PREFIX }),
      onOpenBooking: openBooking,
    });
  }, [signedIn, queryClient]);
}
