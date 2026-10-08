import type { ReactElement } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { ChoiceChipsProps } from '../types/components/ChoiceChips.types';

/**
 * Wrapping row of options where exactly one can be selected, so the photographer picks with a tap instead of typing.
 * @param props Component props.
 * @returns The chips.
 */
export function ChoiceChips({ label, options, value, onChange, error }: ChoiceChipsProps): ReactElement {
  return (
    <View style={styles.container}>
      <Text style={styles.label}>{label}</Text>
      <View style={styles.row}>
        {options.map((option) => {
          const selected = option === value;
          return (
            <Pressable
              key={option}
              accessibilityRole="button"
              accessibilityState={{ selected }}
              accessibilityLabel={option}
              onPress={() => onChange(option)}
              style={[styles.chip, selected && styles.chipSelected, error !== undefined && !selected && styles.chipError]}
            >
              <Text style={[styles.chipText, selected && styles.chipTextSelected]}>{option}</Text>
            </Pressable>
          );
        })}
      </View>
      {error !== undefined && <Text style={styles.error}>{error}</Text>}
    </View>
  );
}

/** Styles of the chips. */
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
    flexWrap: 'wrap',
    gap: spacing.sm,
  },
  chip: {
    borderColor: colors.border,
    borderRadius: radius.pill,
    borderWidth: 1,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
  chipSelected: {
    backgroundColor: colors.primary,
    borderColor: colors.primary,
  },
  chipError: {
    borderColor: colors.danger,
  },
  chipText: {
    color: colors.textPrimary,
    fontSize: 15,
    fontWeight: '600',
  },
  chipTextSelected: {
    color: colors.textInverse,
  },
  error: {
    color: colors.danger,
    fontSize: 13,
  },
});
