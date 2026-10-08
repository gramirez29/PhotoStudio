import { render, screen } from '@testing-library/react-native';
import { StatusBadge } from '../StatusBadge';

describe('StatusBadge', () => {
  it('shows the Spanish label of the status', () => {
    render(<StatusBadge status="ClientAbsent" />);

    expect(screen.getByText('Cliente ausente')).toBeTruthy();
  });

  it('exposes the status to screen readers', () => {
    render(<StatusBadge status="Confirmed" />);

    expect(screen.getByLabelText('Estado: Confirmada')).toBeTruthy();
  });
});
