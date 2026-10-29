import type { ReactElement } from 'react';
import type { RadioGroupFieldProps } from '../types/components/ChoiceFields.types';

/**
 * A group of radio buttons under a legend. The legend and the error are tied to the group for screen readers, and an error is
 * announced when it appears. Nothing is chosen until the client picks: a choice the client did not make is never assumed.
 * @param props Component props.
 * @returns The group.
 */
export function RadioGroupField<Value extends string>({ id, legend, options, value, onChange, error }: RadioGroupFieldProps<Value>): ReactElement {
  const errorId = `${id}-error`;

  return (
    <fieldset className="choice" aria-invalid={error !== undefined} aria-describedby={error === undefined ? undefined : errorId}>
      <legend>{legend}</legend>
      {options.map((option) => (
        <label key={option.value} className="choice__option">
          <input type="radio" name={id} value={option.value} checked={value === option.value} onChange={() => onChange(option.value)} />
          <span>{option.label}</span>
        </label>
      ))}
      {error !== undefined && (
        <p id={errorId} className="field__error" role="alert">
          {error}
        </p>
      )}
    </fieldset>
  );
}
