import { render, screen } from '@testing-library/react-native';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { ContractCard } from '../ContractCard';

describe('ContractCard', () => {
  it('says the contract is pending while it is not signed', () => {
    render(<ContractCard booking={parseBooking({ ...bookingPayload, contract: null })} />);

    expect(screen.getByText('Contrato')).toBeTruthy();
    expect(screen.getByText('Pendiente de firma')).toBeTruthy();
  });

  it('shows only who signed and how for a contract the photographer registered', () => {
    render(<ContractCard booking={parseBooking(bookingPayload)} />);

    expect(screen.getByText(/Firmado por María Pérez en persona el /)).toBeTruthy();
    expect(screen.queryByText(/Cédula/)).toBeNull();
  });

  it('shows what the client declared when they signed through the link', () => {
    const contract = {
      signerName: 'María Pérez',
      templateVersion: 'v1',
      channel: 'Portal',
      signedAt: '2026-10-01T18:00:00+00:00',
      declaration: { idNumber: '112345678', participantCount: 3, sessionLocation: null, imageUse: 'NotAuthorized' },
    };

    render(<ContractCard booking={parseBooking({ ...bookingPayload, contract })} />);

    expect(screen.getByText(/Firmado por María Pérez desde el enlace el /)).toBeTruthy();
    expect(screen.getByText('Cédula o DIMEX: 1-1234-5678')).toBeTruthy();
    expect(screen.getByText('Personas en la sesión: 3')).toBeTruthy();
    expect(screen.getByText('Lugar: por definir con el cliente')).toBeTruthy();
    expect(screen.getByText('Uso de las fotos: No autoriza publicar las fotos')).toBeTruthy();
  });
});
