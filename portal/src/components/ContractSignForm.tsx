import { useState, type FormEvent, type ReactElement } from 'react';
import { MAX_LOCATION_LENGTH, MAX_SIGNER_NAME_LENGTH, validateContractForm } from '../forms/contractForm';
import { useSignContract } from '../hooks/useContract';
import type { ContractSignFormProps } from '../types/components/ContractSteps.types';
import type { ContractFormErrors } from '../types/forms/contractForm.types';
import { describeSignContractError } from '../utils/apiErrors';
import { isSessionValid } from '../utils/proofFlow';
import { CheckboxField } from './CheckboxField';
import { RadioGroupField } from './RadioGroupField';
import { TextField } from './TextField';

/**
 * The part of the contract only the client can fill in, and the signature: who signs, the identity number, how many people
 * come, where, what the photos may be used for, and the acceptance. The signature is the name typed. What the client typed
 * lives in the parent, so it survives a verification that expires halfway.
 * @param props Component props.
 * @returns The form.
 */
export function ContractSignForm({
  token,
  session,
  templateVersion,
  values,
  onChange,
  onSigned,
  onSessionExpired,
  onCancel,
}: ContractSignFormProps): ReactElement {
  const signContract = useSignContract(token);
  const [errors, setErrors] = useState<ContractFormErrors>({});
  const [failure, setFailure] = useState<string | null>(null);

  /**
   * Validates what was typed and signs the contract.
   * @param event Submit event of the form.
   */
  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    setFailure(null);

    const result = validateContractForm(values, templateVersion);
    if (!result.ok) {
      setErrors(result.errors);
      return;
    }

    setErrors({});
    if (!isSessionValid(session, Date.now())) {
      onSessionExpired();
      return;
    }

    signContract.mutate(
      { accessToken: session.accessToken, request: result.request },
      {
        onSuccess: onSigned,
        onError: (error) => {
          const view = describeSignContractError(error);
          if (view.sessionExpired) {
            onSessionExpired();
            return;
          }

          setFailure(view.message);
        },
      },
    );
  }

  return (
    <form className="stack" onSubmit={submit} noValidate aria-label="Firma del contrato">
      <h3>Completa y firma</h3>
      <TextField
        id="signer-name"
        label="Tu nombre completo (tu firma)"
        value={values.signerName}
        onChange={(signerName) => {
          onChange({ ...values, signerName });
          setErrors({ ...errors, signerName: undefined });
        }}
        error={errors.signerName}
        hint="Escribirlo es tu firma. Si firmas por una persona menor de edad, escribe tu nombre como su representante."
        autoComplete="name"
        maxLength={MAX_SIGNER_NAME_LENGTH}
      />
      <TextField
        id="id-number"
        label="Cédula o DIMEX"
        value={values.idNumber}
        onChange={(idNumber) => {
          onChange({ ...values, idNumber });
          setErrors({ ...errors, idNumber: undefined });
        }}
        error={errors.idNumber}
        hint="Por ejemplo 1-1234-5678. Solo aparece en tu contrato."
        inputMode="numeric"
        autoComplete="off"
        maxLength={16}
      />
      <TextField
        id="participants"
        label="¿Cuántas personas vienen a la sesión?"
        value={values.participantCount}
        onChange={(participantCount) => {
          onChange({ ...values, participantCount });
          setErrors({ ...errors, participantCount: undefined });
        }}
        error={errors.participantCount}
        inputMode="numeric"
        autoComplete="off"
        maxLength={3}
      />
      <TextField
        id="location"
        label="Lugar de la sesión (opcional)"
        value={values.sessionLocation}
        onChange={(sessionLocation) => {
          onChange({ ...values, sessionLocation });
          setErrors({ ...errors, sessionLocation: undefined });
        }}
        error={errors.sessionLocation}
        hint="Si todavía no lo definen, déjalo en blanco y lo acuerdan con tu fotógrafo."
        autoComplete="off"
        maxLength={MAX_LOCATION_LENGTH}
      />
      <RadioGroupField
        id="image-use"
        legend="Uso de las fotos (cláusula 12)"
        options={[
          { value: 'Authorized', label: 'Autorizo que el fotógrafo publique fotos de la sesión en su portafolio, redes sociales y material promocional.' },
          { value: 'NotAuthorized', label: 'No autorizo la publicación de las fotos.' },
        ]}
        value={values.imageUse}
        onChange={(imageUse) => {
          onChange({ ...values, imageUse });
          setErrors({ ...errors, imageUse: undefined });
        }}
        error={errors.imageUse}
      />
      <CheckboxField
        id="accepts-terms"
        checked={values.acceptsTerms}
        onChange={(acceptsTerms) => {
          onChange({ ...values, acceptsTerms });
          setErrors({ ...errors, acceptsTerms: undefined });
        }}
        error={errors.acceptsTerms}
      >
        He leído el contrato completo y lo acepto.
      </CheckboxField>

      {failure !== null && (
        <p className="field__error" role="alert">
          {failure}
        </p>
      )}

      <div className="actions">
        <button type="submit" className="button" disabled={signContract.isPending}>
          {signContract.isPending ? 'Firmando…' : 'Firmar contrato'}
        </button>
        <button type="button" className="button button--secondary" onClick={onCancel}>
          Cancelar
        </button>
      </div>
    </form>
  );
}
