import { useState, type ReactElement } from 'react';
import type { TextFieldProps } from '../types/components/TextField.types';

/**
 * Labelled text input. The label, the hint and the error are tied to the input for screen readers, and an error is announced
 * when it appears. A password field has a button at its end that shows what was typed, to check it, and hides it again; it starts
 * hidden every time the field appears.
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
  const [revealed, setRevealed] = useState(false);
  const isPassword = type === 'password';
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
          type={isPassword && revealed ? 'text' : type}
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
        {isPassword && (
          <button
            type="button"
            className="field__toggle"
            aria-label={revealed ? 'Ocultar contraseña' : 'Mostrar contraseña'}
            aria-pressed={revealed}
            onClick={() => setRevealed((current) => !current)}
          >
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z" />
              <circle cx="12" cy="12" r="3" />
              {revealed && <path d="M3 3l18 18" />}
            </svg>
          </button>
        )}
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
