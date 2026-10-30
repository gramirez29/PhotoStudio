import type { ReactElement } from 'react';
import { Switch, Text, View } from 'react-native';
import { useAppTheme } from '../hooks/useAppTheme';
import { useContractForm } from '../hooks/useContractForm';
import { spacing } from '../theme/tokens';
import type { ContractFormProps } from '../types/components/ContractForm.types';
import { imageUseFromChoice, IMAGE_USE_CHOICE_LABELS } from '../utils/contract';
import { formatDateTime } from '../utils/format';
import { CONTRACT_SIGNATURE_LABELS, signatureKindFromLabel } from '../utils/payments';
import { ActionButton } from './ActionButton';
import { ContractText } from './ContractText';
import { FormInput } from './FormInput';
import { KeyboardAwareScreen } from './KeyboardAwareScreen';
import { SelectField } from './SelectField';
import { makeStyles } from '../theme/makeStyles';
import { FONT_FAMILIES } from '../theme/fonts';

/**
 * Screen content that records the signature of the contract. When the client signs on the phone it shows them the contract to
 * read, then asks what only they know (identity number, people, place and what the photos may be used for), their name as the
 * signature, and that they accept; the exact text they read is kept with the signature. A contract on paper only asks for a name.
 * @param props Component props.
 * @returns The form.
 */
export function ContractForm({ booking }: ContractFormProps): ReactElement {
  const { palette } = useAppTheme();
  const styles = useStyles();
  const form = useContractForm(booking);
  const paper = form.values.kind === 'Paper';
  const waitingForContract = form.showsContract && form.contractText === null;

  return (
    <KeyboardAwareScreen align="top">
      <Text style={styles.title}>{booking.clientName}</Text>
      <Text style={styles.subtitle}>
        {booking.packageName} · {formatDateTime(booking.sessionStart)}
      </Text>

      <SelectField
        label="Tipo de firma"
        options={Object.values(CONTRACT_SIGNATURE_LABELS)}
        value={CONTRACT_SIGNATURE_LABELS[form.values.kind]}
        placeholder="Elige cómo se firma"
        onChange={(label) => {
          const kind = signatureKindFromLabel(label);
          if (kind !== null) {
            form.setKind(kind);
          }
        }}
      />

      {form.showsContract && (
        <>
          <Text style={styles.hint}>Entrega el celular al cliente para que lea el contrato y lo firme aquí.</Text>
          {form.isContractLoading && <Text style={styles.hint}>Cargando el contrato…</Text>}
          {form.contractError !== null && (
            <View style={styles.errorBox}>
              <Text style={styles.submitError}>{form.contractError}</Text>
              <ActionButton label="Reintentar" onPress={form.reloadContract} />
            </View>
          )}
          {form.contractText !== null && <ContractText text={form.contractText} />}
        </>
      )}

      {!waitingForContract && (
        <>
          <FormInput
            label={paper ? 'Nombre que aparece en el contrato' : 'Nombre de quien firma (su firma)'}
            value={form.values.signerName}
            onChangeText={(text) => form.setText('signerName', text)}
            error={form.errors.signerName}
            autoCapitalize="words"
          />

          {paper ? (
            <Text style={styles.hint}>
              Toma una foto del contrato firmado y guárdala; aquí solo queda registrado que se firmó en papel.
            </Text>
          ) : (
            <>
              <FormInput
                label="Cédula o DIMEX"
                value={form.values.idNumber}
                onChangeText={(text) => form.setText('idNumber', text)}
                error={form.errors.idNumber}
                keyboardType="number-pad"
                autoCapitalize="none"
                autoCorrect={false}
              />
              <FormInput
                label="¿Cuántas personas vienen a la sesión?"
                value={form.values.participantCount}
                onChangeText={(text) => form.setText('participantCount', text)}
                error={form.errors.participantCount}
                keyboardType="number-pad"
                maxLength={3}
              />
              <FormInput
                label="Lugar de la sesión (opcional)"
                value={form.values.sessionLocation}
                onChangeText={(text) => form.setText('sessionLocation', text)}
                error={form.errors.sessionLocation}
                autoCapitalize="sentences"
              />
              <SelectField
                label="Uso de las fotos (cláusula 12)"
                options={Object.values(IMAGE_USE_CHOICE_LABELS)}
                value={form.values.imageUse === null ? '' : IMAGE_USE_CHOICE_LABELS[form.values.imageUse]}
                placeholder="Elige una opción"
                error={form.errors.imageUse}
                onChange={(label) => {
                  const use = imageUseFromChoice(label);
                  if (use !== null) {
                    form.setChoice('imageUse', use);
                  }
                }}
              />
              <View style={styles.acceptance}>
                <Switch
                  accessibilityLabel="El cliente leyó el contrato y lo acepta"
                  value={form.values.accepted}
                  trackColor={{ false: palette.outlineVariant, true: palette.primary }}
                  thumbColor={form.values.accepted ? palette.onPrimary : palette.outline}
                  onValueChange={(accepted) => form.setChoice('accepted', accepted)}
                />
                <Text style={styles.acceptanceLabel}>El cliente leyó el contrato completo y lo acepta.</Text>
              </View>
              {form.errors.accepted !== undefined && <Text style={styles.submitError}>{form.errors.accepted}</Text>}
            </>
          )}
        </>
      )}

      {form.submitError !== null && <Text style={styles.submitError}>{form.submitError}</Text>}

      <ActionButton
        label={form.isPending ? 'Enviando…' : 'Registrar firma'}
        disabled={form.isPending || waitingForContract}
        onPress={form.submit}
      />
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
  subtitle: {
    color: palette.onSurfaceVariant,
    fontSize: 15,
    fontFamily: FONT_FAMILIES.body,
  },
  hint: {
    color: palette.onSurfaceVariant,
    fontSize: 13,
    fontFamily: FONT_FAMILIES.body,
  },
  errorBox: {
    gap: spacing.sm,
  },
  submitError: {
    color: palette.error,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
  acceptance: {
    alignItems: 'center',
    flexDirection: 'row',
    gap: spacing.sm,
  },
  acceptanceLabel: {
    color: palette.onSurface,
    flex: 1,
    fontSize: 14,
    fontFamily: FONT_FAMILIES.body,
  },
}));
