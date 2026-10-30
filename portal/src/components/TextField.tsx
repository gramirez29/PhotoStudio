import type { ReactElement } from 'react';
import type { TextFieldProps } from '../types/components/TextField.types';

/**
 * Labelled text input. The label, the hint and the error are tied to the input for screen readers, and an error is announced
 * when it appears.
 * @param props Component props.
 * @returns The field.
 */
export function TextField({
  id,
  label,
  value,
  onChange,
  error,
  hint,
  type = 'text',
  inputMode,
  autoComplete,
  maxLength,
  autoFocus = false,
  placeholder,
  prefix,
}: TextFieldProps): ReactElement {
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const described = [hint === undefined ? null : hintId, error === undefined ? null : errorId].filter(Boolean).join(' ');

  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <div className="field__control">
        {prefix !== undefined && (
          <span className="field__prefix" aria-hidden="true">
            {prefix}
          </span>
        )}
        <input
          id={id}
          type={type}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          inputMode={inputMode}
          autoComplete={autoComplete}
          maxLength={maxLength}
          autoFocus={autoFocus}
          placeholder={placeholder}
          aria-invalid={error !== undefined}
          aria-describedby={described.length > 0 ? described : undefined}
        />
      </div>
      {hint !== undefined && (
        <p id={hintId} className="field__hint">
          {hint}
        </p>
      )}
      {error !== undefined && (
        <p id={errorId} className="field__error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
