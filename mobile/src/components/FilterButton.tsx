import { MaterialCommunityIcons } from '@expo/vector-icons';
import type { ReactElement } from 'react';
import { Pressable, Text, View } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { FONT_FAMILIES } from '../theme/fonts';
import { makeStyles } from '../theme/makeStyles';
import { radius, spacing } from '../theme/tokens';
import type { FilterButtonProps } from '../types/components/FilterButton.types';

/** Size of the icon, in points. */
const ICON_SIZE = 20;

/**
 * Button that opens and closes the panel of filters: the funnel icon with the word "Filtrar". When filters are applied, it is
 * tinted and carries the number of criteria, so the photographer knows the list is narrowed even with the panel closed.
 * @param props Component props.
 * @returns The button.
 */
export function FilterButton({ activeCount, open, onPress }: FilterButtonProps): ReactElement {
  const styles = useStyles();
  const { palette } = useAppTheme();
  const highlighted = open || activeCount > 0;
  const color = highlighted ? palette.onPrimaryContainer : palette.onSurface;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={activeCount > 0 ? `Filtrar, ${activeCount} aplicado${activeCount === 1 ? '' : 's'}` : 'Filtrar'}
      accessibilityState={{ expanded: open }}
      onPress={onPress}
      style={({ pressed }) => [styles.button, highlighted && styles.highlighted, pressed && styles.pressed]}
    >
      <MaterialCommunityIcons name="filter-variant" size={ICON_SIZE} color={color} accessible={false} importantForAccessibility="no" />
      <Text style={[styles.label, { color }]}>Filtrar</Text>
      {activeCount > 0 && (
        <View style={styles.count} testID="filter-count">
          <Text style={styles.countText}>{activeCount}</Text>
        </View>
      )}
    </Pressable>
  );
}

/** Styles of the button: the colors come from the theme. */
const useStyles = makeStyles((palette) => ({
  button: {
    alignItems: 'center',
    borderColor: palette.outlineVariant,
    borderRadius: radius.pill,
    borderWidth: 1,
    flexDirection: 'row',
    gap: spacing.xs,
    minHeight: 40,
    paddingHorizontal: spacing.md,
  },
  highlighted: {
    backgroundColor: palette.primaryContainer,
    borderColor: palette.primaryContainer,
  },
  pressed: {
    opacity: 0.7,
  },
  label: {
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 15,
  },
  count: {
    alignItems: 'center',
    backgroundColor: palette.primary,
    borderRadius: radius.pill,
    height: 20,
    justifyContent: 'center',
    minWidth: 20,
    paddingHorizontal: 5,
  },
  countText: {
    color: palette.onPrimary,
    fontFamily: FONT_FAMILIES.bodyMedium,
    fontSize: 12,
  },
}));
