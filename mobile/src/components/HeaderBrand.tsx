import type { ReactElement } from 'react';
import { Image, View } from 'react-native';
import { Text } from 'react-native-paper';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { radius, spacing } from '../theme/tokens';

/** The mark of the logo, already small. */
const LOGO_MARK = require('../../assets/logo-mark.png') as number;

/**
 * The title of the home screen: the logo with its silver border and the name of the studio in letter-spaced capitals, as on the
 * band of the web. The logo is decoration; the name next to it says what it is.
 * @returns The brand.
 */
export function HeaderBrand(): ReactElement {
  const styles = useStyles();

  return (
    <View accessibilityRole="header" accessibilityLabel="PhotoSense Studio" style={styles.row}>
      <Image source={LOGO_MARK} accessible={false} importantForAccessibility="no" style={styles.logo} />
      <Text style={styles.name} accessible={false}>
        PHOTOSENSE STUDIO
      </Text>
    </View>
  );
}

/** Styles of the brand: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  row: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.sm,
  },
  logo: {
    borderColor: palette.silver,
    borderRadius: radius.sm,
    borderWidth: 2,
    height: 34,
    width: 34,
  },
  name: {
    color: palette.onBrand,
    fontFamily: FONT_FAMILIES.title,
    fontSize: 15,
    letterSpacing: 2.5,
  },
}));
