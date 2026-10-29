import DateTimePicker, { DateTimePickerAndroid } from '@react-native-community/datetimepicker';
import type { ReactElement } from 'react';
import { Platform, StyleSheet, View } from 'react-native';
import { Button, HelperText, Text, useTheme } from 'react-native-paper';
import { spacing } from '../theme/tokens';
import type { DateTimeFieldProps, PickerMode } from '../types/components/DateTimeField.types';
import { withDayOf, withTimeOf } from '../utils/date';
import { formatDate, formatTime } from '../utils/format';

/**
 * Field to pick a day and a time. On iOS it renders the native compact pickers inline; on Android it opens the system
 * dialogs from two outlined buttons, because the Android picker is an imperative API.
 * @param props Component props.
 * @returns The field.
 */
export function DateTimeField({ label, value, onChange, minimumDate, error }: DateTimeFieldProps): ReactElement {
  const theme = useTheme();
  const hasError = error !== undefined;
  const errorColors = hasError ? { textColor: theme.colors.error } : {};

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
      <Text variant="labelLarge" style={{ color: hasError ? theme.colors.error : theme.colors.onSurfaceVariant }}>
        {label}
      </Text>
      {Platform.OS === 'android' ? (
        <View style={styles.row}>
          <Button
            mode="outlined"
            accessibilityLabel={`${label}: día`}
            onPress={() => openAndroidPicker('date')}
            style={[styles.dateButton, hasError && { borderColor: theme.colors.error }]}
            contentStyle={styles.buttonContent}
            {...errorColors}
          >
            {formatDate(value)}
          </Button>
          <Button
            mode="outlined"
            accessibilityLabel={`${label}: hora`}
            onPress={() => openAndroidPicker('time')}
            style={hasError ? { borderColor: theme.colors.error } : undefined}
            contentStyle={styles.buttonContent}
            {...errorColors}
          >
            {formatTime(value)}
          </Button>
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
      {hasError && (
        <HelperText type="error" visible>
          {error}
        </HelperText>
      )}
    </View>
  );
}

/** Layout of the field; its colors come from the theme. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.xs,
  },
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.sm,
  },
  dateButton: {
    flex: 1,
  },
  buttonContent: {
    minHeight: 48,
  },
});
