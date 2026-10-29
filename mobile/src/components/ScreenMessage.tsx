import type { ReactElement } from 'react';
import { StyleSheet, View } from 'react-native';
import { Text, useTheme } from 'react-native-paper';
import { spacing } from '../theme/tokens';
import type { ScreenMessageProps } from '../types/components/ScreenMessage.types';

/**
 * Message centered on the whole screen, for a screen that cannot show its content.
 * @param props Component props.
 * @returns The centered message.
 */
export function ScreenMessage({ message }: ScreenMessageProps): ReactElement {
  const theme = useTheme();

  return (
    <View style={styles.container}>
      <Text variant="bodyLarge" style={[styles.message, { color: theme.colors.onSurfaceVariant }]}>
        {message}
      </Text>
    </View>
  );
}

/** Layout of the centered message. */
const styles = StyleSheet.create({
  container: {
    alignItems: 'center',
    flex: 1,
    justifyContent: 'center',
    padding: spacing.lg,
  },
  message: {
    textAlign: 'center',
  },
});
