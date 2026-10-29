import type { ContractFormValues } from '../../types/forms/contractForm.types';
import { initialContractValues, MAX_SIGNER_NAME_LENGTH, readIdDigits, validateContract } from '../contractForm';

/** A form that is valid for a contract signed on the phone. */
const COMPLETE: ContractFormValues = {
  kind: 'InPerson',
  signerName: '  María Pérez  ',
  idNumber: '1-1234-5678',
  participantCount: '3',
  sessionLocation: '  Parque La Sabana  ',
  imageUse: 'NotAuthorized',
  accepted: true,
};

describe('initialContractValues', () => {
  it('starts signing on the phone, with the client as signer and nothing chosen', () => {
    expect(initialContractValues('María Pérez')).toEqual({
      kind: 'InPerson',
      signerName: 'María Pérez',
      idNumber: '',
      participantCount: '',
      sessionLocation: '',
      imageUse: null,
      accepted: false,
    });
  });
});

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

describe('validateContract', () => {
  it('builds the request for a contract signed on the phone, with the declaration and the version the client read', () => {
    expect(validateContract(COMPLETE, 'v7')).toEqual({
      ok: true,
      request: {
        signerName: 'María Pérez',
        templateVersion: 'v7',
        isPaperContract: false,
        declaration: { idNumber: '112345678', participantCount: 3, sessionLocation: 'Parque La Sabana', imageUse: 'NotAuthorized' },
      },
    });
  });

  it('leaves the location out when it is blank', () => {
    const result = validateContract({ ...COMPLETE, sessionLocation: '   ' }, 'v1');

    expect(result.ok && result.request.declaration !== undefined && 'sessionLocation' in result.request.declaration).toBe(false);
  });

  it('marks a paper contract and asks for nothing but the name', () => {
    const result = validateContract({ ...COMPLETE, kind: 'Paper', idNumber: '', participantCount: '', imageUse: null, accepted: false }, '');

    expect(result).toEqual({ ok: true, request: { signerName: 'María Pérez', templateVersion: 'v2', isPaperContract: true } });
  });

  it('asks for the name when it is empty or blank', () => {
    expect(validateContract({ ...COMPLETE, signerName: '   ' }, 'v1')).toMatchObject({
      ok: false,
      errors: { signerName: 'Escribe el nombre de quien firma.' },
    });
    expect(validateContract({ ...COMPLETE, kind: 'Paper', signerName: '' }, '')).toMatchObject({ ok: false });
  });

  it('rejects a name that is too long', () => {
    const result = validateContract({ ...COMPLETE, signerName: 'a'.repeat(MAX_SIGNER_NAME_LENGTH + 1) }, 'v1');

    expect(result).toMatchObject({ ok: false });
  });

  it('asks for everything the client has to give on the phone, each next to its field', () => {
    const result = validateContract(initialContractValues('María Pérez'), 'v1');

    expect(!result.ok && Object.keys(result.errors).sort()).toEqual(['accepted', 'idNumber', 'imageUse', 'participantCount']);
  });

  it.each(['12345678', '1234567890', '1234567890123', '11234567A'])('rejects the identity number %s', (idNumber) => {
    const result = validateContract({ ...COMPLETE, idNumber }, 'v1');

    expect(!result.ok && result.errors.idNumber).toMatch(/cédula/);
  });

  it.each(['15600012345', '155600012345'])('accepts the DIMEX %s', (idNumber) => {
    expect(validateContract({ ...COMPLETE, idNumber }, 'v1').ok).toBe(true);
  });

  it.each(['0', '101', '2.5', '-1', 'tres', ''])('rejects "%s" people', (participantCount) => {
    const result = validateContract({ ...COMPLETE, participantCount }, 'v1');

    expect(!result.ok && result.errors.participantCount).toBeDefined();
  });

  it('rejects a location that is too long', () => {
    const result = validateContract({ ...COMPLETE, sessionLocation: 'a'.repeat(201) }, 'v1');

    expect(!result.ok && result.errors.sessionLocation).toBeDefined();
  });

  it('does not accept the signature until the client confirms they accept the contract', () => {
    const result = validateContract({ ...COMPLETE, accepted: false }, 'v1');

    expect(!result.ok && Object.keys(result.errors)).toEqual(['accepted']);
  });
});
