import type { BookingStatus } from '../api/booking';

/** Props of the `BookingFilterPanel` component. */
export interface BookingFilterPanelProps {
  /** Whether the panel is shown; it opens and closes with a short slide. */
  readonly open: boolean;
  /** The text in the search box. */
  readonly text: string;
  /** The status chosen, or null for any. */
  readonly status: BookingStatus | null;
  /** Called with the new text when the photographer types. */
  readonly onTextChange: (text: string) => void;
  /** Called with a status when the photographer taps its chip. */
  readonly onToggleStatus: (status: BookingStatus) => void;
}
