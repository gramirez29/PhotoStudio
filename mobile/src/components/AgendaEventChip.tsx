import type { ReactElement } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { radius, spacing } from '../theme/tokens';
import type { AgendaEventChipProps } from '../types/components/agenda.types';
import { describeBookingForReader, formatBookingHours } from '../utils/agenda/labels';
import { BOOKING_STATUS_LABELS } from '../utils/labels';
import { STATUS_COLOR_ROLES } from '../utils/statusColors';

/**
 * A booking in the agenda, as a block filled with the color of its status: who, when and what, and the name of the status, because
 * the color is never the only thing that says it. A booking that was cancelled or expired has its name struck through. Tapping it opens
 * the booking.
 * @param props Component props.
 * @returns The block.
 */
export function AgendaEventChip({ booking, onPress }: AgendaEventChipProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const struck = booking.status === 'Cancelled' || booking.status === 'Expired';

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={describeBookingForReader(booking)}
      onPress={() => onPress(booking.id)}
      style={({ pressed }) => [styles.chip, { backgroundColor: palette[STATUS_COLOR_ROLES[booking.status]] }, pressed && styles.pressed]}
    >
      <View style={styles.texts}>
        <Text numberOfLines={1} style={[styles.title, struck && styles.struck]}>
          {booking.clientName}
        </Text>
        <Text numberOfLines={1} style={styles.detail}>
          {formatBookingHours(booking)} · {booking.packageName}
        </Text>
      </View>
      <Text style={styles.status}>{BOOKING_STATUS_LABELS[booking.status]}</Text>
    </Pressable>
  );
}

/** Styles of the block: the text color is the one for the fill of a status, as in the badge. */
const useStyles = makeStyles((palette) => ({
  chip: {
    alignItems: 'center',
    borderRadius: radius.sm,
    flexDirection: 'row',
    gap: spacing.sm,
    minHeight: 48,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
  pressed: {
    opacity: 0.8,
  },
  texts: {
    flex: 1,
  },
  title: {
    color: palette.onError,
    fontFamily: FONT_FAMILIES.bodyBold,
    fontSize: 15,
  },
  struck: {
    textDecorationLine: 'line-through',
  },
  detail: {
    color: palette.onError,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 13,
  },
  status: {
    color: palette.onError,
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 12,
  },
}));
