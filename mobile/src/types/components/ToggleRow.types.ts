/** Props of the `ToggleRow` component. */
export interface ToggleRowProps {
  /** What the row says. */
  readonly label: string;
  /** What a screen reader says when it is longer than the label; the label itself when omitted. */
  readonly accessibilityLabel?: string;
  /** Whether the switch is on. */
  readonly value: boolean;
  /** Called with the value the switch takes when the row is tapped. */
  readonly onValueChange: (value: boolean) => void;
}
