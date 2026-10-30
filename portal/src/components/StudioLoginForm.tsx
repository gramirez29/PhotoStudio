import { useState, type FormEvent, type ReactElement } from 'react';
import { useStudioLogin } from '../hooks/useStudioMutations';
import type { StudioLoginFormProps } from '../types/components/StudioComponents.types';
import { writePendingCode } from '../utils/pendingCode';
import { describeLoginError } from '../utils/studioErrors';
import { StepHeading } from './StepHeading';
import { TextField } from './TextField';

/**
 * The password step of the web area of the photographer. A remembered browser gets in here; any other gets a code in the email
 * and the page moves on to ask for it. The same message answers a wrong password and an unknown user.
 * @param props Component props.
 * @returns The form.
 */
export function StudioLoginForm({ onCodeRequired }: StudioLoginFormProps): ReactElement {
  const login = useStudioLogin();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);

  /**
   * Sends the password.
   * @param event Submit event of the form.
   */
  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    if (username.trim().length === 0 || password.length === 0) {
      setError('Escribe tu usuario y tu contraseña.');
      return;
    }

    setError(null);
    login.mutate(
      { username: username.trim(), password },
      {
        onSuccess: (result) => {
          if (result.status === 'CodeRequired') {
            writePendingCode({ maskedEmail: result.maskedEmail });
            onCodeRequired({ maskedEmail: result.maskedEmail });
          }

          setPassword('');
        },
        onError: (failure) => setError(describeLoginError(failure).message),
      },
    );
  }

  return (
    <section className="card" aria-labelledby="proof-step-title">
      <StepHeading>Entrar a tu estudio</StepHeading>
      <p className="muted">Esta página es solo para el fotógrafo. Si eres cliente, abre el enlace que te envió tu fotógrafo.</p>

      <form className="stack" onSubmit={submit} noValidate>
        <TextField id="username" label="Usuario" value={username} onChange={setUsername} autoComplete="username" autoFocus />
        <TextField id="password" label="Contraseña" type="password" value={password} onChange={setPassword} autoComplete="current-password" />
        {error !== null && (
          <p className="field__error" role="alert">
            {error}
          </p>
        )}
        <div className="actions">
          <button type="submit" className="button" disabled={login.isPending}>
            {login.isPending ? 'Entrando…' : 'Entrar'}
          </button>
        </div>
      </form>
    </section>
  );
}
