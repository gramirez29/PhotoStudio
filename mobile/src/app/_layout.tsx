import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState, type ReactElement } from 'react';
import { createQueryClient } from '../api/queryClient';
import { restoreAppearance } from '../appearance/appearanceManager';
import { AppBackground } from '../components/AppBackground';
import { AppThemeProvider } from '../components/AppThemeProvider';
import { HeaderBrand } from '../components/HeaderBrand';
import { Placeholder } from '../components/Placeholder';
import { PushRegistration } from '../components/PushRegistration';
import { ScreenLoader } from '../components/ScreenLoader';
import { SignOutButton } from '../components/SignOutButton';
import { useAppFonts } from '../hooks/useAppFonts';
import { useAppTheme } from '../hooks/useAppTheme';
import { appScreenLayout } from '../navigation/appScreenLayout';
import { buildHeaderOptions } from '../navigation/headerOptions';
import { restoreSession } from '../session/sessionManager';
import { useSessionStore } from '../session/sessionStore';
import { statusBarStyleFor } from '../utils/appearance';

/**
 * Root layout: provides the query client and the theme (colors, fonts and the background picture) and decides what the
 * photographer sees from the state of the session. Without a session only the login screen exists; with one, only the app
 * screens do, so no route can be reached while signed out.
 * @returns The navigation tree.
 */
export default function RootLayout(): ReactElement {
  const [queryClient] = useState(createQueryClient);
  const status = useSessionStore((state) => state.status);
  const emailVerified = useSessionStore((state) => state.session?.emailVerified === true);
  const fontsReady = useAppFonts();
  const { palette, scheme } = useAppTheme();

  useEffect(() => {
    void restoreSession();
    void restoreAppearance();
  }, []);

  // Whatever was cached belongs to the previous session: it must never be shown to whoever signs in next.
  useEffect(() => {
    if (status === 'signedOut') {
      queryClient.clear();
    }
  }, [status, queryClient]);

  let content: ReactElement;
  if (status === 'restoring' || !fontsReady) {
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
        screenLayout={appScreenLayout}
        screenOptions={{
          ...buildHeaderOptions(palette),
          // Transparent: a screen paints its own background, and what no screen covers shows the one of the whole window.
          contentStyle: { backgroundColor: 'transparent' },
        }}
      >
        <Stack.Protected guard={status === 'signedIn' && emailVerified}>
          <Stack.Screen
            name="index"
            options={{ title: 'PhotoSense Studio', headerTitle: HeaderBrand, headerLeft: SignOutButton }}
          />
          <Stack.Screen name="notifications" options={{ title: 'Avisos' }} />
          <Stack.Screen name="change-password" options={{ title: 'Cambiar contraseña' }} />
          <Stack.Screen name="bookings/new" options={{ title: 'Nueva reserva' }} />
          <Stack.Screen name="bookings/reschedule" options={{ title: 'Reprogramar' }} />
          <Stack.Screen name="refunds" options={{ title: 'Reembolsos' }} />
          <Stack.Screen name="bookings/refund" options={{ title: 'Registrar reembolso' }} />
          <Stack.Screen name="bookings/contract" options={{ title: 'Firmar contrato' }} />
          <Stack.Screen name="bookings/edit" options={{ title: 'Editar reserva' }} />
          <Stack.Screen name="bookings/payment" options={{ title: 'Registrar pago' }} />
          <Stack.Screen name="bookings/reason" options={{ title: 'Motivo' }} />
          <Stack.Screen name="bookings/reject-payment" options={{ title: 'Rechazar comprobante' }} />
          <Stack.Screen name="bookings/[id]" options={{ title: 'Reserva' }} />
        </Stack.Protected>
        <Stack.Protected guard={status === 'signedIn' && !emailVerified}>
          <Stack.Screen name="verify-email" options={{ title: 'Verifica tu correo', headerLeft: SignOutButton, headerBackVisible: false }} />
        </Stack.Protected>
        <Stack.Protected guard={status === 'signedOut'}>
          <Stack.Screen name="login" options={{ title: 'Iniciar sesión', headerShown: false }} />
        </Stack.Protected>
      </Stack>
    );
  }

  return (
    <QueryClientProvider client={queryClient}>
      <AppThemeProvider>
        <StatusBar style={statusBarStyleFor(status === 'signedIn', scheme)} />
        <PushRegistration />
        {/* The background of the whole window: it covers what no screen paints, such as the strip under the system bars. */}
        <AppBackground>{content}</AppBackground>
      </AppThemeProvider>
    </QueryClientProvider>
  );
}
