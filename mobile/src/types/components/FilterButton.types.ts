/** Props of the `FilterButton` component. */
export interface FilterButtonProps {
  /** How many criteria are applied to the list; a mark with the number shows when it is more than zero. */
  readonly activeCount: number;
  /** Whether the panel of filters is open. */
  readonly open: boolean;
  /** Called when the photographer taps the button. */
  readonly onPress: () => void;
}
