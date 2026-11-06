import type { ReactElement } from 'react';
import { SegmentedButtons } from 'react-native-paper';
import type { HomePeriodSelectorProps } from '../types/components/home.types';
import type { HomePeriodKey } from '../types/hooks/useBookingStats.types';
import { HOME_PERIOD_LABELS } from '../utils/home/periods';

/** The periods in the order the selector lists them. */
const PERIODS: readonly HomePeriodKey[] = ['thisMonth', 'lastMonth', 'all'];

/**
 * The selector of the period the chart of the home screen counts: this month, last month or all the time.
 * @param props Component props.
 * @returns The selector.
 */
export function HomePeriodSelector({ value, onChange }: HomePeriodSelectorProps): ReactElement {
  return (
    <SegmentedButtons
      value={value}
      onValueChange={(selected) => {
        const period = PERIODS.find((option) => option === selected);
        if (period !== undefined) {
          onChange(period);
        }
      }}
      buttons={PERIODS.map((option) => ({
        value: option,
        label: HOME_PERIOD_LABELS[option],
        accessibilityLabel: `Periodo: ${HOME_PERIOD_LABELS[option]}`,
      }))}
    />
  );
}
