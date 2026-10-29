import { describe, expect, it } from 'vitest';
import type { ContractFormValues } from '../types/forms/contractForm.types';
import { INITIAL_CONTRACT_VALUES, readIdDigits, validateContractForm } from './contractForm';

/** A form that is valid as it is. */
const VALID: ContractFormValues = {
  signerName: '  María Pérez  ',
  idNumber: '1-1234-5678',
  participantCount: '3',
  sessionLocation: '  Parque La Sabana  ',
  imageUse: 'Authorized',
  acceptsTerms: true,
};

describe('readIdDigits', () => {
  it('keeps the digits of an identity number typed with hyphens or spaces', () => {
    expect(readIdDigits('1-1234-5678')).toBe('112345678');
    expect(readIdDigits(' 1 1234 5678 ')).toBe('112345678');
  });

  it('refuses text with anything else', () => {
    expect(readIdDigits('11234567A')).toBeNull();
    expect(readIdDigits('')).toBeNull();
  });
});

describe('validateContractForm', () => {
  it('builds the request with trimmed text and the digits of the id', () => {
    expect(validateContractForm(VALID, 'v1')).toEqual({
      ok: true,
      request: {
        templateVersion: 'v1',
        signerName: 'María Pérez',
        idNumber: '112345678',
        participantCount: 3,
        sessionLocation: 'Parque La Sabana',
        imageUse: 'Authorized',
        acceptsTerms: true,
      },
    });
  });

  it('leaves the location out when it is blank', () => {
    const result = validateContractForm({ ...VALID, sessionLocation: '   ' }, 'v1');

    expect(result.ok && 'sessionLocation' in result.request).toBe(false);
  });

  it('asks for everything that is missing, each next to its field', () => {
    const result = validateContractForm(INITIAL_CONTRACT_VALUES, 'v1');

    expect(result.ok).toBe(false);
    expect(!result.ok && Object.keys(result.errors).sort()).toEqual([
      'acceptsTerms',
      'idNumber',
      'imageUse',
      'participantCount',
      'signerName',
    ]);
  });

  it.each(['12345678', '1234567890', '1234567890123', '11234567A'])('rejects the identity number %s', (idNumber) => {
    const result = validateContractForm({ ...VALID, idNumber }, 'v1');

    expect(!result.ok && result.errors.idNumber).toMatch(/cédula/);
  });

  it.each(['15600012345', '155600012345'])('accepts the DIMEX %s', (idNumber) => {
    expect(validateContractForm({ ...VALID, idNumber }, 'v1').ok).toBe(true);
  });

  it.each(['0', '101', '2.5', '-1', 'tres', ''])('rejects "%s" people', (participantCount) => {
    const result = validateContractForm({ ...VALID, participantCount }, 'v1');

    expect(!result.ok && result.errors.participantCount).toBeDefined();
  });

  it('rejects a location that is too long', () => {
    const result = validateContractForm({ ...VALID, sessionLocation: 'a'.repeat(201) }, 'v1');

    expect(!result.ok && result.errors.sessionLocation).toBeDefined();
  });

  it('does not sign without accepting the contract or choosing the image use', () => {
    const result = validateContractForm({ ...VALID, acceptsTerms: false, imageUse: null }, 'v1');

    expect(!result.ok && Object.keys(result.errors).sort()).toEqual(['acceptsTerms', 'imageUse']);
  });
});
