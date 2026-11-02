import { describe, expect, it } from 'vitest';
import { isZipFile } from './zipFile';

describe('isZipFile', () => {
  it('accepts what looks like a ZIP by its name or by what the browser says', () => {
    expect(isZipFile({ name: 'Fotos.ZIP', type: '' })).toBe(true);
    expect(isZipFile({ name: 'x', type: 'application/zip' })).toBe(true);
    expect(isZipFile({ name: 'x', type: 'application/x-zip-compressed' })).toBe(true);
  });

  it('refuses everything else', () => {
    expect(isZipFile({ name: 'a.rar', type: 'application/x-rar' })).toBe(false);
    expect(isZipFile({ name: 'zip', type: '' })).toBe(false);
  });
});
