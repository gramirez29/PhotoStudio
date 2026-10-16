import { buildLinkMessage, shareLinkOutcomeMessage } from '../portalLink';

describe('buildLinkMessage', () => {
  const url = 'https://develop.photosensestudio.com/b/abc';

  it('greets the client by first name, names the package and carries the link', () => {
    const message = buildLinkMessage('María Pérez Mora', 'Retrato Familiar', url, true);

    expect(message).toContain('Hola María,');
    expect(message).toContain('Retrato Familiar');
    expect(message).toContain(url);
  });

  it('tells a client with an email that a code will arrive there', () => {
    expect(buildLinkMessage('María Pérez', 'Compromiso', url, true)).toContain('código a tu correo');
  });

  it('promises only the detail when the booking has no email', () => {
    const message = buildLinkMessage('María Pérez', 'Compromiso', url, false);

    expect(message).not.toContain('código');
    expect(message).toContain('ver el detalle');
  });
});

describe('shareLinkOutcomeMessage', () => {
  it('has no message when the link was shared', () => {
    expect(shareLinkOutcomeMessage('shared')).toBeNull();
  });

  it('explains each way sharing can fail', () => {
    expect(shareLinkOutcomeMessage('invalid-phone')).toContain('teléfono');
    expect(shareLinkOutcomeMessage('whatsapp-unavailable')).toContain('WhatsApp');
  });
});
