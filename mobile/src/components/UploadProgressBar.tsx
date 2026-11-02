import type { ReactElement } from 'react';
import { View } from 'react-native';
import type { UploadProgressBarProps } from '../types/components/UploadComponents.types';
import { makeStyles } from '../theme/makeStyles';

/**
 * A thin bar that fills as an upload goes on.
 * @param props Component props.
 * @returns The bar.
 */
export function UploadProgressBar({ fraction, accessibilityLabel }: UploadProgressBarProps): ReactElement {
  const styles = useStyles();
  const clamped = Math.max(0, Math.min(1, fraction));

  return (
    <View style={styles.track} accessibilityRole="progressbar" accessibilityLabel={accessibilityLabel} accessibilityValue={{ min: 0, max: 100, now: Math.round(clamped * 100) }}>
      <View style={[styles.fill, { width: `${clamped * 100}%` }]} />
    </View>
  );
}

/** Styles of the bar. */
const useStyles = makeStyles((palette) => ({
  track: {
    height: 6,
    borderRadius: 3,
    overflow: 'hidden',
    backgroundColor: palette.outlineVariant,
  },
  fill: {
    height: 6,
    backgroundColor: palette.primary,
  },
}));
