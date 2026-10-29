import type { ReactElement } from 'react';
import { StyleSheet, View } from 'react-native';
import { SegmentedButtons, Text, useTheme } from 'react-native-paper';
import { spacing } from '../theme/tokens';
import type { DurationPickerProps } from '../types/components/DurationPicker.types';

/**
 * Row of connected Material buttons to choose how long a session lasts, in hours.
 * @param props Component props.
 * @returns The picker.
 */
export function DurationPicker({ label, options, value, onChange }: DurationPickerProps): ReactElement {
  const theme = useTheme();

  return (
    <View style={styles.container}>
      <Text variant="labelLarge" style={{ color: theme.colors.onSurfaceVariant }}>
        {label}
      </Text>
      <SegmentedButtons
        value={String(value)}
        onValueChange={(selected) => onChange(Number(selected))}
        buttons={options.map((hours) => ({
          value: String(hours),
          label: `${hours} h`,
          accessibilityLabel: `${hours} ${hours === 1 ? 'hora' : 'horas'}`,
        }))}
      />
    </View>
  );
}

/** Layout of the picker; its colors come from the theme. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.xs,
  },
});
