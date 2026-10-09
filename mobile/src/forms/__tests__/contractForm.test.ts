import { MAX_SIGNER_NAME_LENGTH, validateContract } from '../contractForm';

describe('validateContract', () => {
  it('builds the request for a contract signed on the phone', () => {
    expect(validateContract('  María Pérez  ', 'InPerson')).toEqual({
      ok: true,
      request: { signerName: 'María Pérez', templateVersion: 'v1', isPaperContract: false },
    });
  });

  it('marks a paper contract', () => {
    const result = validateContract('María Pérez', 'Paper');

    expect(result).toMatchObject({ ok: true, request: { isPaperContract: true } });
  });

  it('asks for the name when it is empty or blank', () => {
    expect(validateContract('   ', 'InPerson')).toEqual({ ok: false, error: 'Escribe el nombre de quien firma.' });
  });

  it('rejects a name that is too long', () => {
    const result = validateContract('a'.repeat(MAX_SIGNER_NAME_LENGTH + 1), 'InPerson');

    expect(result).toMatchObject({ ok: false });
  });
});
