import { useState, type ReactElement } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { HelperText, Text, useTheme } from 'react-native-paper';
import { spacing } from '../theme/tokens';
import type { SelectFieldProps } from '../types/components/SelectField.types';

/** Height of the field, the same as an outlined Material text field. */
const FIELD_HEIGHT = 56;

/**
 * Drop-down field that looks like the outlined text fields: it shows the selected option under its label and, when tapped,
 * opens a list to pick another one: a sheet on the surface color, with the option chosen filled in the main color of the app.
 * The list is a modal so it looks and behaves the same on iOS and Android.
 * @param props Component props.
 * @returns The field and its list.
 */
export function SelectField({ label, options, value, onChange, placeholder, error }: SelectFieldProps): ReactElement {
  const theme = useTheme();
  const [open, setOpen] = useState(false);
  const hasError = error !== undefined;
  const border = hasError ? theme.colors.error : theme.colors.outline;

  return (
    <View style={styles.container}>
      <Pressable
        accessibilityRole="combobox"
        accessibilityLabel={label}
        accessibilityState={{ expanded: open }}
        accessibilityValue={{ text: value.length > 0 ? value : placeholder }}
        onPress={() => setOpen(true)}
        style={[styles.field, { backgroundColor: theme.colors.background, borderColor: border, borderRadius: theme.roundness }]}
      >
        <Text
          variant="bodyLarge"
          numberOfLines={1}
          style={[styles.value, { color: value.length > 0 ? theme.colors.onSurface : theme.colors.onSurfaceVariant }]}
        >
          {value.length > 0 ? value : placeholder}
        </Text>
        <Text style={[styles.chevron, { color: theme.colors.onSurfaceVariant }]}>▾</Text>
        <Text
          variant="bodySmall"
          style={[styles.label, { backgroundColor: theme.colors.background, color: hasError ? theme.colors.error : theme.colors.onSurfaceVariant }]}
        >
          {label}
        </Text>
      </Pressable>
      {hasError && (
        <HelperText type="error" visible>
          {error}
        </HelperText>
      )}

      <Modal visible={open} transparent animationType="fade" onRequestClose={() => setOpen(false)}>
        <Pressable
          accessibilityLabel="Cerrar lista"
          style={[styles.backdrop, { backgroundColor: theme.colors.backdrop }]}
          onPress={() => setOpen(false)}
        >
          <View style={[styles.sheet, { backgroundColor: theme.colors.surface, borderColor: theme.colors.outlineVariant }]}>
            <Text variant="titleMedium" style={[styles.sheetTitle, { color: theme.colors.onSurface }]}>
              {label}
            </Text>
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
                    style={({ pressed }) => [
                      styles.option,
                      pressed && { backgroundColor: theme.colors.surfaceVariant },
                      selected && { backgroundColor: theme.colors.primary },
                    ]}
                  >
                    <Text variant="bodyLarge" style={{ color: selected ? theme.colors.onPrimary : theme.colors.onSurface }}>
                      {option}
                    </Text>
                    {selected && <Text style={[styles.check, { color: theme.colors.onPrimary }]}>✓</Text>}
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

/** Layout of the field and its list; its colors come from the theme. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.xs,
  },
  field: {
    alignItems: 'center',
    borderWidth: 1,
    flexDirection: 'row',
    height: FIELD_HEIGHT,
    justifyContent: 'space-between',
    paddingHorizontal: spacing.md,
  },
  label: {
    left: spacing.sm + spacing.xs,
    paddingHorizontal: spacing.xs,
    position: 'absolute',
    top: -10,
  },
  value: {
    flex: 1,
  },
  chevron: {
    fontSize: 16,
    marginLeft: spacing.sm,
  },
  backdrop: {
    flex: 1,
    justifyContent: 'center',
    padding: spacing.lg,
  },
  sheet: {
    borderRadius: 28,
    borderWidth: 1,
    maxHeight: '70%',
    paddingVertical: spacing.md,
  },
  sheetTitle: {
    paddingBottom: spacing.sm,
    paddingHorizontal: spacing.lg,
  },
  option: {
    alignItems: 'center',
    borderRadius: 16,
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginHorizontal: spacing.sm,
    minHeight: 48,
    paddingHorizontal: spacing.md,
  },
  check: {
    fontSize: 16,
    fontWeight: '700',
  },
});
