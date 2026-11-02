import { useRef, type ChangeEvent, type ReactElement } from 'react';
import { FINAL_ACCEPT } from '../constants/finals';
import type { FinalsPickerProps } from '../types/components/ProofComponents.types';

/**
 * The button that opens the file picker for the final photos. It offers JPEG files only (the API checks what really arrives, this
 * only saves the photographer a mistake) and tells how the files are matched with the photos the client chose.
 * @param props Component props.
 * @returns The picker.
 */
export function FinalsPicker({ disabled, onChoose }: FinalsPickerProps): ReactElement {
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

  return (
    <div className="stack stack--tight">
      <input ref={input} type="file" accept={FINAL_ACCEPT} multiple hidden aria-label="Elegir las fotos finales" onChange={handleChange} />
      <button type="button" className="button" disabled={disabled} onClick={() => input.current?.click()}>
        Elegir las fotos finales
      </button>
      <p className="field__hint">
        Solo JPEG, de hasta 50 MB cada una. Cada archivo se empareja con la foto que eligió el cliente por su nombre: `IMG_0001.jpg` o
        `IMG_0001-Edit.jpg` van con `IMG_0001`.
      </p>
    </div>
  );
}
