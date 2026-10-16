import { useEffect } from 'react';
import { usePushRegistration } from '../hooks/usePushRegistration';
import { configureForegroundNotifications } from '../push/pushManager';

/**
 * Invisible component that keeps the app connected to push notifications. It lives inside the query provider because it
 * refreshes the inbox when a notification arrives.
 * @returns Nothing: it draws no UI.
 */
export function PushRegistration(): null {
  useEffect(() => {
    configureForegroundNotifications();
  }, []);

  usePushRegistration();
  return null;
}
