import { fireEvent, render, screen } from '@testing-library/react-native';
import { changeAgendaShowAll, changeAgendaView } from '../../agenda/agendaManager';
import { useAgendaStore } from '../../agenda/agendaStore';
import { AgendaHeaderActions } from '../AgendaHeaderActions';
import { AgendaHeaderTitle } from '../AgendaHeaderTitle';
import { AgendaViewMenu } from '../AgendaViewMenu';

jest.mock('../../agenda/agendaManager', () => ({ changeAgendaView: jest.fn(), changeAgendaShowAll: jest.fn(), restoreAgendaPreferences: jest.fn() }));

/**
 * Puts the agenda in a known state.
 * @param state Parts of the state to set.
 */
function agendaIs(state: Partial<ReturnType<typeof useAgendaStore.getState>>): void {
  useAgendaStore.setState({
    view: 'schedule',
    anchor: new Date(2026, 10, 11),
    visibleDate: new Date(2026, 10, 11),
    showAll: false,
    ...state,
  });
}

describe('AgendaHeaderTitle', () => {
  beforeEach(() => {
    jest.useFakeTimers().setSystemTime(new Date(2026, 10, 11, 10, 0));
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('names the month of what the photographer is looking at, as a header', () => {
    agendaIs({});

    render(<AgendaHeaderTitle />);

    expect(screen.getByRole('header')).toBeTruthy();
    expect(screen.getByText('Noviembre')).toBeTruthy();
  });

  it('follows the day at the top of the list as it scrolls', () => {
    agendaIs({ visibleDate: new Date(2026, 11, 3) });

    render(<AgendaHeaderTitle />);

    expect(screen.getByText('Diciembre')).toBeTruthy();
  });

  it('adds the year when it is not the current one', () => {
    agendaIs({ visibleDate: new Date(2027, 0, 5) });

    render(<AgendaHeaderTitle />);

    expect(screen.getByText('Enero 2027')).toBeTruthy();
  });

  it('uses the anchor, not the scroll, in the views that are not the list, which are the ones a phase adds', () => {
    // A view without a screen yet falls back to the list, so the scroll decides; this guards the day a view is added.
    agendaIs({ view: 'month', visibleDate: new Date(2026, 11, 3), anchor: new Date(2026, 10, 11) });

    render(<AgendaHeaderTitle />);

    expect(screen.getByText('Diciembre')).toBeTruthy();
  });
});

describe('AgendaViewMenu', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    agendaIs({});
  });

  it('draws nothing while it is closed', () => {
    render(<AgendaViewMenu visible={false} onClose={jest.fn()} />);

    expect(screen.queryByText('Vista')).toBeNull();
  });

  it('lists the five views and ticks the one that is open', () => {
    render(<AgendaViewMenu visible onClose={jest.fn()} />);

    for (const label of ['Agenda', 'Día', '3 días', 'Semana', 'Mes']) {
      expect(screen.getByText(label)).toBeTruthy();
    }

    expect(screen.getByRole('menuitem', { name: 'Agenda' }).props.accessibilityState).toMatchObject({ selected: true, disabled: false });
  });

  it('dims the views that have no screen yet and does not let them be chosen', () => {
    render(<AgendaViewMenu visible onClose={jest.fn()} />);

    const week = screen.getByRole('menuitem', { name: 'Semana, todavía no disponible' });
    fireEvent.press(week);

    expect(week.props.accessibilityState).toMatchObject({ disabled: true });
    expect(changeAgendaView).not.toHaveBeenCalled();
  });

  it('changes the view and closes when one is chosen', () => {
    const onClose = jest.fn();
    render(<AgendaViewMenu visible onClose={onClose} />);

    fireEvent.press(screen.getByRole('menuitem', { name: 'Agenda' }));

    expect(changeAgendaView).toHaveBeenCalledWith('schedule');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('shows the switch of the ended bookings off by default and turns it on without closing', () => {
    const onClose = jest.fn();
    render(<AgendaViewMenu visible onClose={onClose} />);
    const toggle = screen.getByLabelText(/Mostrar todas las reservas/);

    expect(toggle.props.value).toBe(false);
    fireEvent(toggle, 'valueChange', true);

    expect(changeAgendaShowAll).toHaveBeenCalledWith(true);
    expect(onClose).not.toHaveBeenCalled();
  });

  it('shows the switch on when the ended bookings are shown', () => {
    agendaIs({ showAll: true });

    render(<AgendaViewMenu visible onClose={jest.fn()} />);

    expect(screen.getByLabelText(/Mostrar todas las reservas/).props.value).toBe(true);
  });

  it('closes when the dimmed screen is tapped', () => {
    const onClose = jest.fn();
    render(<AgendaViewMenu visible onClose={onClose} />);

    fireEvent.press(screen.getByRole('button', { name: 'Cerrar el menú de vistas' }));

    expect(onClose).toHaveBeenCalledTimes(1);
  });
});

describe('AgendaHeaderActions', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers().setSystemTime(new Date(2026, 10, 11, 10, 0));
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('brings the agenda back to today', () => {
    agendaIs({ anchor: new Date(2027, 2, 5), visibleDate: new Date(2027, 2, 5) });
    render(<AgendaHeaderActions />);

    fireEvent.press(screen.getByRole('button', { name: 'Ir a hoy' }));

    expect(useAgendaStore.getState().anchor).toEqual(new Date(2026, 10, 11));
    expect(useAgendaStore.getState().visibleDate).toEqual(new Date(2026, 10, 11));
  });

  it('opens the menu of views', () => {
    agendaIs({});
    render(<AgendaHeaderActions />);
    expect(screen.queryByText('Vista')).toBeNull();

    fireEvent.press(screen.getByRole('button', { name: 'Elegir la vista de la agenda' }));

    expect(screen.getByText('Vista')).toBeTruthy();
  });

  it('closes the menu again', () => {
    agendaIs({});
    render(<AgendaHeaderActions />);
    fireEvent.press(screen.getByRole('button', { name: 'Elegir la vista de la agenda' }));

    fireEvent.press(screen.getByRole('button', { name: 'Cerrar el menú de vistas' }));

    expect(screen.queryByText('Vista')).toBeNull();
  });
});
