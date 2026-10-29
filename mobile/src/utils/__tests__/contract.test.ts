import type { ClientDeclarationResponse, ContractResponse } from '../../types/api/booking';
import { describeContract, describeDeclaration, formatIdNumber } from '../contract';

const DECLARATION: ClientDeclarationResponse = {
  idNumber: '112345678',
  participantCount: 3,
  sessionLocation: 'Parque La Sabana',
  imageUse: 'Authorized',
};

/**
 * Builds a contract signature with optional overrides.
 * @param overrides Values to replace.
 * @returns The signature.
 */
function contract(overrides: Partial<ContractResponse> = {}): ContractResponse {
  return {
    signerName: 'María Pérez',
    templateVersion: 'v1',
    channel: 'Portal',
    signedAt: '2026-10-01T18:00:00+00:00',
    declaration: DECLARATION,
    ...overrides,
  };
}

describe('formatIdNumber', () => {
  it('writes a cédula of 9 digits as 1-1234-5678', () => {
    expect(formatIdNumber('112345678')).toBe('1-1234-5678');
  });

  it('leaves a DIMEX, or anything else, as it was saved', () => {
    expect(formatIdNumber('155600012345')).toBe('155600012345');
    expect(formatIdNumber('15600012345')).toBe('15600012345');
    expect(formatIdNumber('abc')).toBe('abc');
  });
});

describe('describeDeclaration', () => {
  it('lists the id, the people, the place and what the photos may be used for', () => {
    expect(describeDeclaration(DECLARATION)).toEqual([
      'Cédula o DIMEX: 1-1234-5678',
      'Personas en la sesión: 3',
      'Lugar: Parque La Sabana',
      'Uso de las fotos: Autoriza publicar las fotos',
    ]);
  });

  it('says the place is still to be agreed when the client left it blank', () => {
    expect(describeDeclaration({ ...DECLARATION, sessionLocation: null })).toContain('Lugar: por definir con el cliente');
  });

  it('says when the client does not allow publishing', () => {
    expect(describeDeclaration({ ...DECLARATION, imageUse: 'NotAuthorized' })).toContain('Uso de las fotos: No autoriza publicar las fotos');
  });
});

describe('describeContract', () => {
  it('says it is pending while there is no signature', () => {
    expect(describeContract(null)).toEqual(['Pendiente de firma']);
  });

  it('says who signed, how, when, and what the client declared, for a contract signed through the link', () => {
    const lines = describeContract(contract());

    expect(lines[0]).toMatch(/^Firmado por María Pérez desde el enlace el /);
    expect(lines).toHaveLength(5);
    expect(lines).toContain('Cédula o DIMEX: 1-1234-5678');
  });

  it.each([
    ['InPerson', 'en persona'],
    ['External', 'en papel'],
    ['PhotographerApp', 'desde la app'],
  ] as const)('says a contract signed %s was signed %s and shows no declaration', (channel, label) => {
    const lines = describeContract(contract({ channel, declaration: null }));

    expect(lines).toHaveLength(1);
    expect(lines[0]).toContain(`Firmado por María Pérez ${label} el `);
  });
});
