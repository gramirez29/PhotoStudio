import { useRef, type ChangeEvent, type ReactElement } from 'react';
import { PROOF_ACCEPT } from '../constants/proof';
import type { ProofPickerProps } from '../types/components/ProofComponents.types';

/**
 * The button that opens the file picker for the photos of the proofs. It says how many more fit in the gallery, and offers JPEG
 * files only (the API checks what really arrives, this only saves the photographer a mistake).
 * @param props Component props.
 * @returns The picker.
 */
export function ProofPicker({ room, disabled, onChoose }: ProofPickerProps): ReactElement {
  const input = useRef<HTMLInputElement>(null);

  /**
   * Hands the chosen files over and clears the input, so choosing the same files again still counts as a change.
   * @param event Change event of the input.
   */
  function handleChange(event: ChangeEvent<HTMLInputElement>): void {
    const files = Array.from(event.target.files ?? []);
    event.target.value = '';
    if (files.length > 0) {
      onChoose(files);
    }
  }

  if (room <= 0) {
    return <p className="muted">La galería ya tiene el máximo de pruebas. Quita alguna si quieres cambiarla por otra.</p>;
  }

  return (
    <div className="stack stack--tight">
      <input ref={input} type="file" accept={PROOF_ACCEPT} multiple hidden aria-label="Elegir fotos" onChange={handleChange} />
      <button type="button" className="button" disabled={disabled} onClick={() => input.current?.click()}>
        Elegir fotos
      </button>
      <p className="field__hint">Solo JPEG. Puedes subir hasta {room} {room === 1 ? 'foto' : 'fotos'} más.</p>
    </div>
  );
}
