import type { ReactElement } from 'react';
import { Text, View } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import type { GalleryStatusPillProps } from '../types/components/GalleryComponents.types';
import { describeGalleryStatus } from '../utils/gallery';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';
import { spacing } from '../theme/tokens';

/**
 * The state of a gallery as a pill with its color, like the state of a booking. The color is never the only thing that says it: the
 * words are always there.
 * @param props Component props.
 * @returns The pill.
 */
export function GalleryStatusPill({ gallery }: GalleryStatusPillProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const view = describeGalleryStatus(gallery.status);
  const color = palette[view.tone];

  return (
    <View style={[styles.pill, { backgroundColor: color }]} accessibilityLabel={`Estado de la galería: ${view.label}`}>
      <Text style={styles.label}>{view.label}</Text>
    </View>
  );
}

/** Styles of the pill. */
const useStyles = makeStyles((palette) => ({
  pill: {
    alignSelf: 'flex-start',
    borderRadius: 999,
    paddingHorizontal: spacing.sm,
    paddingVertical: 4,
  },
  label: {
    color: palette.onPrimary,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.bodyBold,
  },
}));
