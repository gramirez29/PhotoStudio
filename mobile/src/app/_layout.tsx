import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState, type ReactElement } from 'react';
import { createQueryClient } from '../api/queryClient';
import { NewBookingButton } from '../components/NewBookingButton';
import { Placeholder } from '../components/Placeholder';
import { ScreenLoader } from '../components/ScreenLoader';
import { SignOutButton } from '../components/SignOutButton';
import { restoreSession } from '../session/sessionManager';
import { useSessionStore } from '../session/sessionStore';
import { colors } from '../theme/tokens';

/**
 * Root layout: provides the query client and decides what the photographer sees from the state of the session. Without a
 * session only the login screen exists; with one, only the app screens do, so no route can be reached while signed out.
 * @returns The navigation tree.
 */
export default function RootLayout(): ReactElement {
  const [queryClient] = useState(createQueryClient);
  const status = useSessionStore((state) => state.status);

  useEffect(() => {
    void restoreSession();
  }, []);

  // Whatever was cached belongs to the previous session: it must never be shown to whoever signs in next.
  useEffect(() => {
    if (status === 'signedOut') {
      queryClient.clear();
    }
  }, [status, queryClient]);

  let content: ReactElement;
  if (status === 'restoring') {
    content = <ScreenLoader accessibilityLabel="Abriendo tu sesión" />;
  } else if (status === 'offline') {
    content = (
      <Placeholder
        message="No se pudo conectar para abrir tu sesión. Revisa la conexión e inténtalo de nuevo."
        onRetry={() => void restoreSession()}
      />
    );
  } else {
    content = (
      <Stack
        screenOptions={{
          headerTitleAlign: 'center',
          headerTintColor: colors.textPrimary,
          contentStyle: { backgroundColor: colors.background },
        }}
      >
        <Stack.Protected guard={status === 'signedIn'}>
          <Stack.Screen
            name="index"
            options={{ title: 'PhotoStud.io', headerLeft: SignOutButton, headerRight: NewBookingButton }}
          />
          <Stack.Screen name="notifications" options={{ title: 'Avisos' }} />
          <Stack.Screen name="bookings/new" options={{ title: 'Nueva reserva' }} />
          <Stack.Screen name="bookings/reschedule" options={{ title: 'Reprogramar' }} />
          <Stack.Screen name="bookings/reason" options={{ title: 'Motivo' }} />
          <Stack.Screen name="bookings/[id]" options={{ title: 'Reserva' }} />
        </Stack.Protected>
        <Stack.Protected guard={status === 'signedOut'}>
          <Stack.Screen name="login" options={{ title: 'Iniciar sesión', headerShown: false }} />
        </Stack.Protected>
      </Stack>
    );
  }

  return (
    <QueryClientProvider client={queryClient}>
      <StatusBar style="dark" />
      {content}
    </QueryClientProvider>
  );
}
