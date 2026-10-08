import { render, screen } from '@testing-library/react-native';
import { StatusBadge } from '../StatusBadge';

describe('StatusBadge', () => {
  it('shows the Spanish label of the status', () => {
    render(<StatusBadge status="NoShow" />);

    expect(screen.getByText('No se presentó')).toBeTruthy();
  });

  it('exposes the status to screen readers', () => {
    render(<StatusBadge status="Confirmed" />);

    expect(screen.getByLabelText('Estado: Confirmada')).toBeTruthy();
  });
});
