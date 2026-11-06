import { fireEvent, render, screen } from '@testing-library/react-native';
import { formatTime } from '../../utils/format';
import { atHour, novemberBooking } from '../../utils/agenda/__fixtures__/agendaFixtures';
import { AgendaEventChip } from '../AgendaEventChip';
import { ScheduleDayRow } from '../ScheduleDayRow';
import { ScheduleEdgeButton } from '../ScheduleEdgeButton';
import { ScheduleMonthBanner } from '../ScheduleMonthBanner';

describe('AgendaEventChip', () => {
  it('shows who, when and what, and the name of the status', () => {
    render(<AgendaEventChip booking={novemberBooking('a', 11, 15, 17)} onPress={jest.fn()} />);

    expect(screen.getByText('Client a')).toBeTruthy();
    expect(screen.getByText(`${formatTime(atHour(11, 15))} – ${formatTime(atHour(11, 17))} · Retrato Familiar`)).toBeTruthy();
    expect(screen.getByText('Confirmada')).toBeTruthy();
  });

  it('is read by a screen reader as one sentence', () => {
    render(<AgendaEventChip booking={novemberBooking('a', 11, 15, 17, 'Tentative')} onPress={jest.fn()} />);

    expect(
      screen.getByRole('button', {
        name: `Reserva de Client a, Retrato Familiar, de ${formatTime(atHour(11, 15))} a ${formatTime(atHour(11, 17))}, tentativa`,
      }),
    ).toBeTruthy();
  });

  it('opens the booking by its identifier when it is tapped', () => {
    const onPress = jest.fn();
    render(<AgendaEventChip booking={novemberBooking('abc', 11, 9, 10)} onPress={onPress} />);

    fireEvent.press(screen.getByRole('button'));

    expect(onPress).toHaveBeenCalledWith('abc');
  });

  it('strikes the name through for a booking that was cancelled or expired, and not for the others', () => {
    const { rerender } = render(<AgendaEventChip booking={novemberBooking('a', 11, 9, 10, 'Cancelled')} onPress={jest.fn()} />);
    expect(screen.getByText('Client a')).toHaveStyle({ textDecorationLine: 'line-through' });

    rerender(<AgendaEventChip booking={novemberBooking('a', 11, 9, 10, 'Expired')} onPress={jest.fn()} />);
    expect(screen.getByText('Client a')).toHaveStyle({ textDecorationLine: 'line-through' });

    rerender(<AgendaEventChip booking={novemberBooking('a', 11, 9, 10, 'Confirmed')} onPress={jest.fn()} />);
    expect(screen.getByText('Client a')).not.toHaveStyle({ textDecorationLine: 'line-through' });
  });

  it.each([
    ['Tentative', 'Tentativa'],
    ['Confirmed', 'Confirmada'],
    ['Completed', 'Completada'],
    ['Cancelled', 'Cancelada'],
    ['Expired', 'Vencida'],
    ['ClientAbsent', 'Cliente ausente'],
  ] as const)('names the status %s as %s, so the color is never the only thing that says it', (status, label) => {
    render(<AgendaEventChip booking={novemberBooking('a', 11, 9, 10, status)} onPress={jest.fn()} />);

    expect(screen.getByText(label)).toBeTruthy();
  });
});

describe('ScheduleDayRow', () => {
  const day = (isToday: boolean, bookings = [novemberBooking('a', 11, 9, 10), novemberBooking('b', 11, 11, 12)]) => ({
    date: new Date(2026, 10, 11),
    key: '2026-11-11',
    isToday,
    bookings,
  });

  it('shows the weekday, the number and the bookings of the day', () => {
    render(<ScheduleDayRow section={day(false)} onOpenBooking={jest.fn()} />);

    expect(screen.getByText('mié')).toBeTruthy();
    expect(screen.getByText('11')).toBeTruthy();
    expect(screen.getByText('Client a')).toBeTruthy();
    expect(screen.getByText('Client b')).toBeTruthy();
  });

  it('says the whole date to a screen reader, and that it is today', () => {
    const { rerender } = render(<ScheduleDayRow section={day(false)} onOpenBooking={jest.fn()} />);
    expect(screen.getByRole('header').props.accessibilityLabel).toMatch(/miércoles, 11 de noviembre de 2026$/);

    rerender(<ScheduleDayRow section={day(true)} onOpenBooking={jest.fn()} />);
    expect(screen.getByRole('header').props.accessibilityLabel).toMatch(/2026, hoy$/);
  });

  it('shows a quiet line for a day without bookings', () => {
    render(<ScheduleDayRow section={day(true, [])} onOpenBooking={jest.fn()} />);

    expect(screen.getByText('Sin reservas')).toBeTruthy();
  });

  it('opens a booking when it is tapped', () => {
    const onOpenBooking = jest.fn();
    render(<ScheduleDayRow section={day(false)} onOpenBooking={onOpenBooking} />);

    fireEvent.press(screen.getAllByRole('button')[1] as never);

    expect(onOpenBooking).toHaveBeenCalledWith('b');
  });
});

describe('ScheduleMonthBanner', () => {
  it('names the month as a header', () => {
    render(<ScheduleMonthBanner label="Diciembre 2027" />);

    expect(screen.getByRole('header')).toBeTruthy();
    expect(screen.getByText('Diciembre 2027')).toBeTruthy();
  });
});

describe('ScheduleEdgeButton', () => {
  it('asks for more days when it is tapped', () => {
    const onPress = jest.fn();
    render(<ScheduleEdgeButton label="Ver reservas posteriores" onPress={onPress} loading={false} />);

    fireEvent.press(screen.getByRole('button', { name: 'Ver reservas posteriores' }));

    expect(onPress).toHaveBeenCalledTimes(1);
  });

  it('shows a spinner instead of the label, and ignores taps, while it loads', () => {
    const onPress = jest.fn();
    render(<ScheduleEdgeButton label="Ver reservas posteriores" onPress={onPress} loading />);

    expect(screen.queryByText('Ver reservas posteriores')).toBeNull();
    fireEvent.press(screen.getByRole('button', { name: 'Ver reservas posteriores' }));

    expect(onPress).not.toHaveBeenCalled();
  });
});
