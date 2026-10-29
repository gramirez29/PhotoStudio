import type { ReactElement } from 'react';
import type { CheckboxFieldProps } from '../types/components/ChoiceFields.types';

/**
 * A labelled checkbox. The label is the whole line, so the touch target is large, and an error is announced when it appears.
 * @param props Component props.
 * @returns The checkbox.
 */
export function CheckboxField({ id, children, checked, onChange, error }: CheckboxFieldProps): ReactElement {
  const errorId = `${id}-error`;

  return (
    <div className="choice">
      <label className="choice__option" htmlFor={id}>
        <input
          id={id}
          type="checkbox"
          checked={checked}
          onChange={(event) => onChange(event.target.checked)}
          aria-invalid={error !== undefined}
          aria-describedby={error === undefined ? undefined : errorId}
        />
        <span>{children}</span>
      </label>
      {error !== undefined && (
        <p id={errorId} className="field__error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
