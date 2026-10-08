import { QueryClientProvider } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useState, type ReactElement } from 'react';
import { createQueryClient } from '../api/queryClient';
import { NewBookingButton } from '../components/NewBookingButton';
import { colors } from '../theme/tokens';

/**
 * Root layout: provides the query client and the stack navigator for every screen.
 * @returns The navigation tree.
 */
export default function RootLayout(): ReactElement {
  const [queryClient] = useState(createQueryClient);

  return (
    <QueryClientProvider client={queryClient}>
      <StatusBar style="dark" />
      <Stack
        screenOptions={{
          headerTitleAlign: 'center',
          headerTintColor: colors.textPrimary,
          contentStyle: { backgroundColor: colors.background },
        }}
      >
        <Stack.Screen
          name="index"
          options={{ title: 'PhotoStud.io', headerRight: NewBookingButton }}
        />
        <Stack.Screen name="bookings/new" options={{ title: 'Nueva reserva' }} />
        <Stack.Screen name="bookings/reschedule" options={{ title: 'Reprogramar' }} />
        <Stack.Screen name="bookings/reason" options={{ title: 'Motivo' }} />
        <Stack.Screen name="bookings/[id]" options={{ title: 'Reserva' }} />
      </Stack>
    </QueryClientProvider>
  );
}
