import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { useGalleryTermsForm } from '../hooks/useGalleryTermsForm';
import type { GalleryTermsFormProps } from '../types/components/GalleryComponents.types';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * The form that changes the terms of a gallery: how many photos the package includes, how many are a gift and what an extra photo
 * costs. It is the way to give photos to a client. It can be changed until the client sends their selection, because from then on the
 * selection was made with the old terms.
 * @param props Component props.
 * @returns The form.
 */
export function GalleryTermsForm({ gallery }: GalleryTermsFormProps): ReactElement {
  const styles = useStyles();
  const form = useGalleryTermsForm(gallery);

  return (
    <KeyboardAwareScreen align="top">
      <Text style={styles.title}>{gallery.clientName}</Text>
      <Text style={styles.hint}>Se pueden cambiar hasta que el cliente envíe su selección.</Text>
      <FormInput label="Fotos incluidas en el paquete" value={form.included} onChangeText={form.setIncluded} keyboardType="number-pad" />
      <FormInput label="Fotos de regalo" value={form.bonus} onChangeText={form.setBonus} keyboardType="number-pad" />
      <FormInput label="Precio de cada foto extra (₡)" value={form.price} onChangeText={form.setPrice} keyboardType="number-pad" error={form.validationError ?? undefined} />
      {form.submitError !== null && <Text style={styles.error}>{form.submitError}</Text>}
      <ActionButton label={form.isPending ? 'Guardando…' : 'Guardar'} disabled={form.isPending} onPress={form.submit} />
    </KeyboardAwareScreen>
  );
}

/** Styles of the form. */
const useStyles = makeStyles((palette) => ({
  title: {
    color: palette.onSurface,
    fontSize: 22,
    fontFamily: FONT_FAMILIES.titleBold,
  },
  hint: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
  error: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
}));
