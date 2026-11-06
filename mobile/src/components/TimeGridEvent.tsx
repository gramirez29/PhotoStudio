import type { ReactElement } from 'react';
import { Pressable, Text } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { radius } from '../theme/tokens';
import type { TimeGridEventProps } from '../types/components/agenda.types';
import { describeBookingForReader, formatBookingHours } from '../utils/agenda/labels';
import { eventFrame } from '../utils/agenda/timeGrid';
import { STATUS_COLOR_ROLES } from '../utils/statusColors';

/** Height under which a block only has room for the name of the client. */
const ROOM_FOR_HOURS = 44;

/**
 * A booking in the grid of hours: a block as tall as the session lasts, filled with the color of its status, with the name of the
 * client and, when it is tall enough, the hours. A cancelled or expired booking has its name struck through. Tapping it opens it.
 * @param props Component props.
 * @returns The block, placed in the column of its day.
 */
export function TimeGridEvent({ event, columnWidth, onPress }: TimeGridEventProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const { booking } = event;
  const frame = eventFrame(event, columnWidth);
  const struck = booking.status === 'Cancelled' || booking.status === 'Expired';
  const roomy = frame.height >= ROOM_FOR_HOURS;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={describeBookingForReader(booking)}
      onPress={() => onPress(booking.id)}
      style={({ pressed }) => [
        styles.block,
        { ...frame, backgroundColor: palette[STATUS_COLOR_ROLES[booking.status]] },
        pressed && styles.pressed,
      ]}
    >
      <Text numberOfLines={roomy ? 2 : 1} style={[styles.title, struck && styles.struck]}>
        {booking.clientName}
      </Text>
      {roomy && (
        <Text numberOfLines={1} style={styles.detail}>
          {formatBookingHours(booking)}
        </Text>
      )}
    </Pressable>
  );
}

/** Styles of the block: the text color is the one for the fill of a status, as in the badge. */
const useStyles = makeStyles((palette) => ({
  block: {
    borderColor: palette.background,
    borderRadius: radius.sm,
    borderWidth: 1,
    overflow: 'hidden',
    paddingHorizontal: 4,
    paddingVertical: 2,
    position: 'absolute',
  },
  pressed: {
    opacity: 0.8,
  },
  title: {
    color: palette.onError,
    fontFamily: FONT_FAMILIES.bodyBold,
    fontSize: 12,
  },
  struck: {
    textDecorationLine: 'line-through',
  },
  detail: {
    color: palette.onError,
    fontFamily: FONT_FAMILIES.body,
    fontSize: 11,
  },
}));
