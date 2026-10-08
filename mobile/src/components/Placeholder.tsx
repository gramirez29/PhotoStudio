import type { ReactElement } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { PlaceholderProps } from '../types/components/Placeholder.types';

/**
 * Message for the states without items to show (empty, error, not configured), with an optional retry button.
 * @param props Component props.
 * @returns The message block.
 */
export function Placeholder({ message, onRetry }: PlaceholderProps): ReactElement {
  return (
    <View style={styles.container}>
      <Text style={styles.message}>{message}</Text>
      {onRetry !== undefined && (
        <Pressable
          accessibilityRole="button"
          onPress={onRetry}
          style={({ pressed }) => [styles.button, pressed && styles.buttonPressed]}
        >
          <Text style={styles.buttonLabel}>Reintentar</Text>
        </Pressable>
      )}
    </View>
  );
}

/** Styles of the message block. */
const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    gap: spacing.md,
    paddingVertical: spacing.lg,
  },
  message: {
    color: colors.textSecondary,
    fontSize: 15,
    textAlign: 'center',
  },
  button: {
    alignItems: 'center',
    backgroundColor: colors.primary,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
  buttonPressed: {
    opacity: 0.8,
  },
  buttonLabel: {
    color: colors.textInverse,
    fontSize: 16,
    fontWeight: '600',
  },
});
