import { useState, type ReactElement } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { SelectFieldProps } from '../types/components/SelectField.types';

/**
 * Drop-down field: it shows the selected option and, when tapped, opens a list to pick another one. The list is a modal
 * so it looks and behaves the same on iOS and Android.
 * @param props Component props.
 * @returns The field and its list.
 */
export function SelectField({ label, options, value, onChange, placeholder, error }: SelectFieldProps): ReactElement {
  const [open, setOpen] = useState(false);

  return (
    <View style={styles.container}>
      <Text style={styles.label}>{label}</Text>
      <Pressable
        accessibilityRole="combobox"
        accessibilityLabel={label}
        accessibilityState={{ expanded: open }}
        accessibilityValue={{ text: value.length > 0 ? value : placeholder }}
        onPress={() => setOpen(true)}
        style={[styles.field, error !== undefined && styles.fieldError]}
      >
        <Text style={[styles.value, value.length === 0 && styles.placeholder]} numberOfLines={1}>
          {value.length > 0 ? value : placeholder}
        </Text>
        <Text style={styles.chevron}>▾</Text>
      </Pressable>
      {error !== undefined && <Text style={styles.error}>{error}</Text>}

      <Modal visible={open} transparent animationType="fade" onRequestClose={() => setOpen(false)}>
        <Pressable accessibilityLabel="Cerrar lista" style={styles.backdrop} onPress={() => setOpen(false)}>
          <View style={styles.sheet}>
            <Text style={styles.sheetTitle}>{label}</Text>
            <ScrollView>
              {options.map((option) => {
                const selected = option === value;
                return (
                  <Pressable
                    key={option}
                    accessibilityRole="menuitem"
                    accessibilityState={{ selected }}
                    accessibilityLabel={option}
                    onPress={() => {
                      onChange(option);
                      setOpen(false);
                    }}
                    style={({ pressed }) => [styles.option, selected && styles.optionSelected, pressed && styles.optionPressed]}
                  >
                    <Text style={[styles.optionText, selected && styles.optionTextSelected]}>{option}</Text>
                    {selected && <Text style={styles.check}>✓</Text>}
                  </Pressable>
                );
              })}
            </ScrollView>
          </View>
        </Pressable>
      </Modal>
    </View>
  );
}

/** Styles of the field and its list. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.xs,
  },
  label: {
    color: colors.textPrimary,
    fontSize: 14,
    fontWeight: '600',
  },
  field: {
    alignItems: 'center',
    borderColor: colors.border,
    borderRadius: radius.sm,
    borderWidth: 1,
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.md,
  },
  fieldError: {
    borderColor: colors.danger,
  },
  value: {
    color: colors.textPrimary,
    flex: 1,
    fontSize: 16,
  },
  placeholder: {
    color: colors.muted,
  },
  chevron: {
    color: colors.textSecondary,
    fontSize: 16,
    marginLeft: spacing.sm,
  },
  error: {
    color: colors.danger,
    fontSize: 13,
  },
  backdrop: {
    backgroundColor: colors.overlay,
    flex: 1,
    justifyContent: 'center',
    padding: spacing.lg,
  },
  sheet: {
    backgroundColor: colors.background,
    borderRadius: radius.md,
    maxHeight: '70%',
    paddingVertical: spacing.sm,
  },
  sheetTitle: {
    color: colors.textSecondary,
    fontSize: 13,
    fontWeight: '600',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
  option: {
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.md,
  },
  optionSelected: {
    backgroundColor: colors.surface,
  },
  optionPressed: {
    opacity: 0.7,
  },
  optionText: {
    color: colors.textPrimary,
    fontSize: 16,
  },
  optionTextSelected: {
    fontWeight: '700',
  },
  check: {
    color: colors.success,
    fontSize: 16,
    fontWeight: '700',
  },
});
