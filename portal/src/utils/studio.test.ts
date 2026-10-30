import { describe, expect, it } from 'vitest';
import { ApiError, NetworkError } from '../api/httpClient';
import { ResponseShapeError } from '../api/guards';
import { galleryPayload } from '../test/fixtures';
import { clearPendingCode, readPendingCode, writePendingCode } from './pendingCode';
import { resolveRoute } from './route';
import { describeExtraPhoto, describeGalleryStatus, describePhotos } from './studio';
import { describeCodeError, describeLoginError } from './studioErrors';

describe('the route of the web area', () => {
  it('is at /studio, with or without the slash, and only there', () => {
    expect(resolveRoute('/studio')).toEqual({ kind: 'studio' });
    expect(resolveRoute('/studio/')).toEqual({ kind: 'studio' });
    expect(resolveRoute('/studio/other')).toEqual({ kind: 'notFound' });
    expect(resolveRoute('/studios')).toEqual({ kind: 'notFound' });
    expect(resolveRoute('/')).toEqual({ kind: 'home' });
  });
});

describe('the words of a gallery', () => {
  it('says where a gallery is, with a tone for the dot', () => {
    expect(describeGalleryStatus('Pending')).toEqual({ label: 'Esperando las pruebas', tone: 'warning' });
    expect(describeGalleryStatus('Delivered').tone).toBe('success');
    expect(describeGalleryStatus('Archived').tone).toBe('neutral');
  });

  it('writes the photos of the package, and the gift only when there is one', () => {
    expect(describePhotos({ ...galleryPayload, allowedActions: [] })).toBe('30 fotos incluidas · 5 de regalo');
    expect(describePhotos({ ...galleryPayload, bonusPhotos: 0, allowedActions: [] })).toBe('30 fotos incluidas');
    expect(describePhotos({ ...galleryPayload, includedPhotos: 1, bonusPhotos: 0, allowedActions: [] })).toBe('1 foto incluida');
  });

  it('writes the price of an extra photo in colones', () => {
    expect(describeExtraPhoto({ ...galleryPayload, allowedActions: [] })).toMatch(/^Foto extra: ₡\s?3[\s\u00a0]?000/);
  });
});

describe('the errors of the sign-in', () => {
  /**
   * Builds an error of the API.
   * @param status HTTP status.
   * @param code Stable code.
   * @param retryAfter Seconds to wait.
   * @returns The error.
   */
  const api = (status: number, code: string | null, retryAfter: number | null = null): ApiError => new ApiError(status, 'x', code, retryAfter);

  it('says the same thing for a wrong password and an unknown user', () => {
    expect(describeLoginError(api(401, 'auth.invalid_credentials')).message).toBe('El usuario o la contraseña no son correctos.');
  });

  it('says how long to wait after too many failures, in the unit that reads best', () => {
    expect(describeLoginError(api(429, 'auth.account_locked', 900))).toMatchObject({ message: expect.stringContaining('15 minutos'), retryAfterSeconds: 900 });
    expect(describeLoginError(api(429, 'auth.login_code_rate_limited', 40)).message).toContain('40 segundos');
    expect(describeLoginError(api(429, 'auth.account_locked')).message).toContain('Espera un rato');
  });

  it('explains an account that cannot use the web area, an unauthorized page and a code that could not be sent', () => {
    expect(describeLoginError(api(403, 'auth.web_requires_verified_email')).message).toContain('verifica el correo');
    expect(describeLoginError(api(403, 'request.forbidden_origin')).message).toContain('no está autorizada');
    expect(describeLoginError(api(503, 'email.delivery_failed')).message).toContain('No pudimos enviar el código');
    expect(describeLoginError(api(429, 'rate_limit.exceeded', 30)).retryAfterSeconds).toBe(30);
  });

  it('explains a lost connection, an answer it did not understand and what nobody planned for', () => {
    expect(describeLoginError(new NetworkError()).message).toContain('No pudimos conectarnos');
    expect(describeLoginError(new ResponseShapeError('x', 'y')).message).toContain('no se entendieron');
    expect(describeLoginError(new Error('boom')).message).toContain('Algo salió mal');
  });

  it('explains a bad code and never says which way it was bad', () => {
    expect(describeCodeError(api(422, 'auth.invalid_login_code')).message).toBe('El código no es válido o ya venció. Pide uno nuevo, o vuelve a empezar.');
    expect(describeCodeError(api(429, 'auth.login_code_rate_limited', 45))).toMatchObject({ retryAfterSeconds: 45, message: expect.stringContaining('45 segundos') });
    expect(describeCodeError(new NetworkError()).message).toContain('No pudimos conectarnos');
    expect(describeCodeError(new Error('boom')).message).toContain('Algo salió mal');
  });
});

describe('the code that is waiting', () => {
  /**
   * Builds a storage that keeps one value in memory.
   * @returns The storage.
   */
  function memory(): Storage {
    const data = new Map<string, string>();
    return {
      getItem: (key) => data.get(key) ?? null,
      setItem: (key, value) => void data.set(key, value),
      removeItem: (key) => void data.delete(key),
    } as Storage;
  }

  it('is remembered, read back and forgotten', () => {
    const storage = memory();
    expect(readPendingCode(storage)).toBeNull();

    writePendingCode({ maskedEmail: 'a***@example.com' }, storage);
    expect(readPendingCode(storage)).toEqual({ maskedEmail: 'a***@example.com' });

    clearPendingCode(storage);
    expect(readPendingCode(storage)).toBeNull();
  });

  it('counts anything that is not what the page wrote as nothing waiting', () => {
    const storage = memory();
    for (const junk of ['not json', '{}', '{"maskedEmail":5}', 'null', '"text"']) {
      storage.setItem('photosense.studio.pending-code', junk);
      expect(readPendingCode(storage)).toBeNull();
    }
  });

  it('does not fail where the storage is blocked', () => {
    const blocked = {
      getItem: () => {
        throw new Error('blocked');
      },
      setItem: () => {
        throw new Error('blocked');
      },
      removeItem: () => {
        throw new Error('blocked');
      },
    };

    expect(readPendingCode(blocked)).toBeNull();
    expect(() => writePendingCode({ maskedEmail: 'x' }, blocked)).not.toThrow();
    expect(() => clearPendingCode(blocked)).not.toThrow();
  });
});
