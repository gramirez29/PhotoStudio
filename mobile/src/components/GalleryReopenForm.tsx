import type { ReactElement } from 'react';
import { Text } from 'react-native';
import { useGalleryReopenForm } from '../hooks/useGalleryReopenForm';
import type { GalleryReopenFormProps } from '../types/components/GalleryComponents.types';
import { ActionButton } from './ActionButton';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * The form that asks why the selection is reopened. Reopening discards what the client chose and lets them choose again; the client
 * gets an email without a link and without the reason. The finals already uploaded stay tied to their photo.
 * @param props Component props.
 * @returns The form.
 */
export function GalleryReopenForm({ gallery }: GalleryReopenFormProps): ReactElement {
  const styles = useStyles();
  const form = useGalleryReopenForm(gallery);

  return (
    <KeyboardAwareScreen align="top">
      <Text style={styles.title}>{gallery.clientName}</Text>
      <Text style={styles.hint}>
        Lo que eligió el cliente se descarta y podrá elegir de nuevo; recibirá un correo. Los finales que ya subiste se conservan para cada
        foto. Solo se puede antes de publicar los finales.
      </Text>
      <FormInput
        label="Motivo (solo lo ves tú)"
        value={form.text}
        onChangeText={form.setText}
        error={form.validationError ?? undefined}
        placeholder="Por ejemplo: el cliente quiere cambiar dos fotos"
        multiline
        numberOfLines={3}
        autoCapitalize="sentences"
      />
      {form.submitError !== null && <Text style={styles.error}>{form.submitError}</Text>}
      <ActionButton label={form.isPending ? 'Reabriendo…' : 'Reabrir la selección'} variant="danger" disabled={form.isPending} onPress={form.submit} />
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
