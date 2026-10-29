import type { ReactElement } from 'react';
import { StyleSheet, View } from 'react-native';
import { spacing } from '../theme/tokens';
import type { PackageFieldsProps } from '../types/components/PackageFields.types';
import { PACKAGE_LABELS, packageLabel } from '../utils/pricing';
import { FormInput } from './FormInput';
import { SelectField } from './SelectField';

/**
 * The package the client booked and the price of the package. Picking a package that has a price fills it in; the photographer
 * can still change it.
 * @param props Component props.
 * @returns The two fields.
 */
export function PackageFields({ packageTier, price, packageError, priceError, onPackageChange, onPriceChange }: PackageFieldsProps): ReactElement {
  return (
    <View style={styles.container}>
      <SelectField
        label="Paquete"
        options={PACKAGE_LABELS}
        value={packageLabel(packageTier)}
        onChange={onPackageChange}
        placeholder="Selecciona un paquete"
        error={packageError}
      />
      <FormInput
        label="Precio (₡)"
        value={price}
        onChangeText={onPriceChange}
        error={priceError}
        keyboardType="number-pad"
        placeholder="80000"
      />
    </View>
  );
}

/** Styles of the fields. */
const styles = StyleSheet.create({
  container: {
    gap: spacing.md,
  },
});
