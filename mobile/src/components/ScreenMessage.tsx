import type { ReactElement } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { colors, spacing } from '../theme/tokens';
import type { ScreenMessageProps } from '../types/components/ScreenMessage.types';

/**
 * Message centered on the whole screen, for a screen that cannot show its content.
 * @param props Component props.
 * @returns The centered message.
 */
export function ScreenMessage({ message }: ScreenMessageProps): ReactElement {
  return (
    <View style={styles.container}>
      <Text style={styles.message}>{message}</Text>
    </View>
  );
}

/** Styles of the centered message. */
const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    flex: 1,
    justifyContent: 'center',
    padding: spacing.lg,
  },
  message: {
    color: colors.textSecondary,
    fontSize: 15,
    textAlign: 'center',
  },
});
