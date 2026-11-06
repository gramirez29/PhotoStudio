import type { ReactElement } from 'react';
import { Text, View } from 'react-native';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { spacing } from '../theme/tokens';
import type { ScheduleMonthBannerProps } from '../types/components/agenda.types';

/**
 * The banner that opens each month in the list view, with its name and a line under it, like the month headers of Google Calendar.
 * @param props Component props.
 * @returns The banner.
 */
export function ScheduleMonthBanner({ label }: ScheduleMonthBannerProps): ReactElement {
  const styles = useStyles();

  return (
    <View accessible accessibilityRole="header" style={styles.banner}>
      <Text style={styles.label}>{label}</Text>
    </View>
  );
}

/** Styles of the banner: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  banner: {
    borderBottomColor: palette.outlineVariant,
    borderBottomWidth: 1,
    marginTop: spacing.md,
    paddingBottom: spacing.xs,
  },
  label: {
    color: palette.onSurface,
    fontFamily: FONT_FAMILIES.titleBold,
    fontSize: 16,
  },
}));
