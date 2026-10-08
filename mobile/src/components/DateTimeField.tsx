import DateTimePicker, { DateTimePickerAndroid } from '@react-native-community/datetimepicker';
import type { ReactElement } from 'react';
import { Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, radius, spacing } from '../theme/tokens';
import type { DateTimeFieldProps, PickerMode } from '../types/components/DateTimeField.types';
import { withDayOf, withTimeOf } from '../utils/date';
import { formatDate, formatTime } from '../utils/format';

/**
 * Field to pick a day and a time. On iOS it renders the native compact pickers inline; on Android it opens the system
 * dialogs from two buttons, because the Android picker is an imperative API.
 * @param props Component props.
 * @returns The field.
 */
export function DateTimeField({ label, value, onChange, minimumDate, error }: DateTimeFieldProps): ReactElement {
  /**
   * Applies a picked value to the part of the instant it edits.
   * @param mode Part being edited.
   * @param picked Value reported by the picker.
   */
  function apply(mode: PickerMode, picked: Date): void {
    onChange(mode === 'date' ? withDayOf(value, picked) : withTimeOf(value, picked));
  }

  /**
   * Opens the Android system dialog for one part of the instant.
   * @param mode Part to edit.
   */
  function openAndroidPicker(mode: PickerMode): void {
    DateTimePickerAndroid.open({
      value,
      mode,
      minimumDate: mode === 'date' ? minimumDate : undefined,
      onValueChange: (_event, picked) => apply(mode, picked),
    });
  }

  return (
    <View style={styles.container}>
      <Text style={styles.label}>{label}</Text>
      {Platform.OS === 'android' ? (
        <View style={styles.row}>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={`${label}: día`}
            onPress={() => openAndroidPicker('date')}
            style={[styles.button, styles.dateButton, error !== undefined && styles.buttonError]}
          >
            <Text style={styles.buttonText} numberOfLines={1}>
              {formatDate(value)}
            </Text>
          </Pressable>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={`${label}: hora`}
            onPress={() => openAndroidPicker('time')}
            style={[styles.button, error !== undefined && styles.buttonError]}
          >
            <Text style={styles.buttonText}>{formatTime(value)}</Text>
          </Pressable>
        </View>
      ) : (
        <View style={styles.row}>
          <DateTimePicker
            value={value}
            mode="date"
            display="compact"
            minimumDate={minimumDate}
            onValueChange={(_event, picked) => apply('date', picked)}
          />
          <DateTimePicker
            value={value}
            mode="time"
            display="compact"
            onValueChange={(_event, picked) => apply('time', picked)}
          />
        </View>
      )}
      {error !== undefined && <Text style={styles.error}>{error}</Text>}
    </View>
  );
}

/** Styles of the field. */
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
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.sm,
  },
  button: {
    borderColor: colors.border,
    borderRadius: radius.sm,
    borderWidth: 1,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
  dateButton: {
    flex: 1,
  },
  buttonError: {
    borderColor: colors.danger,
  },
  buttonText: {
    color: colors.textPrimary,
    fontSize: 16,
  },
  error: {
    color: colors.danger,
    fontSize: 13,
  },
});
