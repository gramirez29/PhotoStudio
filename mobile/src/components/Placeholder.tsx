import type { ReactElement } from 'react';
import { StyleSheet, View } from 'react-native';
import { Button, Text, useTheme } from 'react-native-paper';
import { spacing } from '../theme/tokens';
import type { PlaceholderProps } from '../types/components/Placeholder.types';

/**
 * Message for the states without items to show (empty, error, not configured), with an optional retry button.
 * @param props Component props.
 * @returns The message block.
 */
export function Placeholder({ message, onRetry }: PlaceholderProps): ReactElement {
  const theme = useTheme();

  return (
    <View style={styles.container}>
      <Text variant="bodyLarge" style={[styles.message, { color: theme.colors.onSurfaceVariant }]}>
        {message}
      </Text>
      {onRetry !== undefined && (
        <Button mode="contained" onPress={onRetry} contentStyle={styles.buttonContent}>
          Reintentar
        </Button>
      )}
    </View>
  );
}

/** Layout of the message block; its colors come from the theme. */
const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    gap: spacing.md,
    paddingVertical: spacing.lg,
  },
  message: {
    textAlign: 'center',
  },
  buttonContent: {
    minHeight: 48,
    paddingHorizontal: spacing.md,
  },
});
