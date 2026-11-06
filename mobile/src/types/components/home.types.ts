import type { BookingStatsResponse } from '../api/stats';
import type { HomePeriodKey } from '../hooks/useBookingStats.types';
import type { DonutSegment } from '../utils/home.types';

/** Props of the `StatusDonut` component. */
export interface StatusDonutProps {
  /** The slices to draw; none draws an empty ring. */
  readonly segments: readonly DonutSegment[];
  /** Total of bookings, written in the middle of the ring. */
  readonly total: number;
  /** What a screen reader says about the whole chart. */
  readonly accessibilityLabel: string;
}

/** Props of the `StatusLegend` component. */
export interface StatusLegendProps {
  /** The counts by status; the statuses without bookings are left out. */
  readonly stats: BookingStatsResponse;
}

/** Props of the `HomePeriodSelector` component. */
export interface HomePeriodSelectorProps {
  /** The period that is selected. */
  readonly value: HomePeriodKey;
  /** Called with the period the photographer picks. */
  readonly onChange: (period: HomePeriodKey) => void;
}

/** Props of the `HomeLinkRow` component. */
export interface HomeLinkRowProps {
  /** Name of the icon of the row. */
  readonly icon: 'event' | 'list-alt' | 'add-circle-outline' | 'notifications-none' | 'payments' | 'open-in-new';
  /** Text of the row. */
  readonly label: string;
  /** What the row says at the right, such as a count; nothing when omitted. */
  readonly detail?: string;
  /** What a screen reader says; the label when omitted. */
  readonly accessibilityLabel?: string;
  /** Called when the row is tapped. */
  readonly onPress: () => void;
}
