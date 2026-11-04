import { useEffect, useState, type ReactElement } from 'react';
import { Animated, type LayoutChangeEvent, View } from 'react-native';
import { Chip } from 'react-native-paper';
import { BOOKING_STATUSES } from '../constants/booking';
import { MAX_NAME_FILTER_LENGTH } from '../constants/pagination';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import type { BookingFilterPanelProps } from '../types/components/BookingFilterPanel.types';
import { BOOKING_STATUS_LABELS } from '../utils/labels';
import { FormInput } from './FormInput';

/** Milliseconds the panel takes to open or close: short enough to feel immediate. */
const SLIDE_MS = 200;

/**
 * Panel with the filters of the list of bookings: a search box for the client name and a chip for each status. It opens and
 * closes with a short slide that also fades it. It stays mounted while closed, so the text typed is not lost; closed, it takes no
 * space, cannot be tapped and is hidden from the screen reader.
 * @param props Component props.
 * @returns The panel.
 */
export function BookingFilterPanel({ open, text, status, onTextChange, onToggleStatus }: BookingFilterPanelProps): ReactElement {
  const styles = useStyles();
  const [progress] = useState(() => new Animated.Value(open ? 1 : 0));
  const [contentHeight, setContentHeight] = useState(0);

  useEffect(() => {
    // The height cannot run on the native driver, but the panel is small and the slide lasts a fraction of a second.
    Animated.timing(progress, { toValue: open ? 1 : 0, duration: SLIDE_MS, useNativeDriver: false }).start();
  }, [open, progress]);

  /**
   * Remembers how tall the content is, which is the height the panel slides to.
   * @param event Layout event of the content.
   */
  const measure = (event: LayoutChangeEvent): void => {
    setContentHeight(event.nativeEvent.layout.height);
  };

  return (
    <Animated.View
      testID="booking-filter-panel"
      style={[styles.panel, { height: progress.interpolate({ inputRange: [0, 1], outputRange: [0, contentHeight] }), opacity: progress }]}
      pointerEvents={open ? 'auto' : 'none'}
      accessibilityElementsHidden={!open}
      importantForAccessibility={open ? 'auto' : 'no-hide-descendants'}
    >
      <View style={styles.content} onLayout={measure}>
        <FormInput
          label="Buscar por nombre"
          value={text}
          onChangeText={onTextChange}
          autoCapitalize="words"
          autoCorrect={false}
          returnKeyType="search"
          maxLength={MAX_NAME_FILTER_LENGTH}
        />
        <View style={styles.chips} accessibilityRole="radiogroup" accessibilityLabel="Estado de la reserva">
          {BOOKING_STATUSES.map((value) => (
            <Chip
              key={value}
              selected={status === value}
              showSelectedCheck
              onPress={() => onToggleStatus(value)}
              accessibilityRole="radio"
              accessibilityState={{ checked: status === value }}
            >
              {BOOKING_STATUS_LABELS[value]}
            </Chip>
          ))}
        </View>
      </View>
    </Animated.View>
  );
}

/** Styles of the panel: the content hangs from the top of a box whose height is animated. */
const useStyles = makeStyles(() => ({
  panel: {
    overflow: 'hidden',
  },
  content: {
    gap: spacing.sm,
    left: 0,
    paddingBottom: spacing.sm,
    paddingTop: spacing.xs,
    position: 'absolute',
    right: 0,
    top: 0,
  },
  chips: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.sm,
  },
}));
