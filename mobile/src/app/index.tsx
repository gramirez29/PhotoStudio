import { router } from 'expo-router';
import { useState, type ReactElement } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useApiHealth } from '../hooks/useApiHealth';
import { useRecentBookingsStore } from '../state/recentBookingsStore';
import { colors, radius, spacing } from '../theme/tokens';

/**
 * Opens the detail screen of a booking.
 * @param bookingId Booking identifier.
 */
function openBooking(bookingId: string): void {
  router.push({ pathname: '/bookings/[id]', params: { id: bookingId } });
}

/**
 * Shows whether the backend is reachable and ready.
 * @returns The status line.
 */
function ApiStatus(): ReactElement {
  const health = useApiHealth();

  let label = 'Verificando conexión con el servidor…';
  let color: string = colors.textSecondary;

  if (health.isError) {
    label = 'Sin conexión con el servidor';
    color = colors.danger;
  } else if (health.data === true) {
    label = 'Servidor conectado';
    color = colors.success;
  } else if (health.data === false) {
    label = 'Servidor disponible, base de datos sin conexión';
    color = colors.warning;
  }

  return <Text style={[styles.status, { color }]}>{label}</Text>;
}

/**
 * Home screen: API status, open a booking by identifier and the recently opened bookings.
 * @returns The screen.
 */
export default function HomeScreen(): ReactElement {
  const [bookingId, setBookingId] = useState('');
  const recentBookingIds = useRecentBookingsStore((state) => state.bookingIds);
  const trimmedId = bookingId.trim();

  return (
    <ScrollView contentContainerStyle={styles.container} keyboardShouldPersistTaps="handled">
      <ApiStatus />

      <Text style={styles.sectionTitle}>Abrir una reserva</Text>
      <TextInput
        value={bookingId}
        onChangeText={setBookingId}
        placeholder="Identificador de la reserva"
        placeholderTextColor={colors.muted}
        autoCapitalize="none"
        autoCorrect={false}
        style={styles.input}
        accessibilityLabel="Identificador de la reserva"
      />
      <Pressable
        accessibilityRole="button"
        disabled={trimmedId.length === 0}
        onPress={() => openBooking(trimmedId)}
        style={({ pressed }) => [
          styles.button,
          trimmedId.length === 0 && styles.buttonDisabled,
          pressed && styles.buttonPressed,
        ]}
      >
        <Text style={styles.buttonLabel}>Abrir</Text>
      </Pressable>

      {recentBookingIds.length > 0 && (
        <View style={styles.recent}>
          <Text style={styles.sectionTitle}>Abiertas recientemente</Text>
          {recentBookingIds.map((id) => (
            <Pressable key={id} accessibilityRole="link" onPress={() => openBooking(id)} style={styles.recentItem}>
              <Text style={styles.recentText} numberOfLines={1}>
                {id}
              </Text>
            </Pressable>
          ))}
        </View>
      )}
    </ScrollView>
  );
}

/** Styles of the home screen. */
const styles = StyleSheet.create({
  container: {
    padding: spacing.md,
    gap: spacing.sm,
  },
  status: {
    fontSize: 14,
    marginBottom: spacing.md,
  },
  sectionTitle: {
    color: colors.textPrimary,
    fontSize: 17,
    fontWeight: '700',
    marginTop: spacing.sm,
  },
  input: {
    borderColor: colors.border,
    borderRadius: radius.sm,
    borderWidth: 1,
    color: colors.textPrimary,
    fontSize: 15,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
  button: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: radius.sm,
    paddingVertical: spacing.md,
  },
  buttonDisabled: {
    opacity: 0.4,
  },
  buttonPressed: {
    opacity: 0.8,
  },
  buttonLabel: {
    color: colors.textInverse,
    fontSize: 16,
    fontWeight: '600',
  },
  recent: {
    gap: spacing.xs,
    marginTop: spacing.md,
  },
  recentItem: {
    backgroundColor: colors.surface,
    borderRadius: radius.sm,
    padding: spacing.md,
  },
  recentText: {
    color: colors.textPrimary,
    fontSize: 14,
  },
});
