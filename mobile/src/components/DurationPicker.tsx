import type { ReactElement } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { DurationPickerProps } from '../types/components/DurationPicker.types';

/**
 * Row of options to choose how long a session lasts, in hours.
 * @param props Component props.
 * @returns The picker.
 */
export function DurationPicker({ label, options, value, onChange }: DurationPickerProps): ReactElement {
  return (
    <View style={styles.container}>
      <Text style={styles.label}>{label}</Text>
      <View style={styles.row}>
        {options.map((hours) => {
          const selected = value === hours;
          return (
            <Pressable
              key={hours}
              accessibilityRole="button"
              accessibilityState={{ selected }}
              accessibilityLabel={`${hours} ${hours === 1 ? 'hora' : 'horas'}`}
              onPress={() => onChange(hours)}
              style={[styles.chip, selected && styles.chipSelected]}
            >
              <Text style={[styles.chipText, selected && styles.chipTextSelected]}>{hours} h</Text>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

/** Styles of the picker. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.xs,
  },
  label: {
    color: colors.textPrimary,
    fontSize: 14,
    fontWeight: '600',
  },
  row: {
    flexDirection: 'row',
    gap: spacing.sm,
  },
  chip: {
    alignItems: 'center',
    borderColor: colors.border,
    borderRadius: radius.pill,
    borderWidth: 1,
    flex: 1,
    paddingVertical: spacing.sm,
  },
  chipSelected: {
    backgroundColor: colors.primary,
    borderColor: colors.primary,
  },
  chipText: {
    color: colors.textPrimary,
    fontSize: 15,
    fontWeight: '600',
  },
  chipTextSelected: {
    color: colors.textInverse,
  },
});
