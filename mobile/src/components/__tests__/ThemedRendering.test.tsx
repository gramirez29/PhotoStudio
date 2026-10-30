import { fireEvent, render, screen } from '@testing-library/react-native';
import type { ReactElement } from 'react';
import { parseBooking } from '../../api/bookingsApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { useAppearanceStore } from '../../appearance/appearanceStore';
import { BOOKING_STATUSES } from '../../constants/booking';
import { DARK_PALETTE, LIGHT_PALETTE } from '../../theme/palettes';
import type { ColorScheme } from '../../types/theme/palette.types';
import { useMenuStore } from '../../menu/menuStore';
import { useSessionStore } from '../../session/sessionStore';
import { ActionButton } from '../ActionButton';
import { AmountRow } from '../AmountRow';
import { AppCard } from '../AppCard';
import { AppMenu } from '../AppMenu';
import { AppThemeProvider } from '../AppThemeProvider';
import { ContractCard } from '../ContractCard';
import { DurationPicker } from '../DurationPicker';
import { ExtraRow } from '../ExtraRow';
import { ExtrasModal } from '../ExtrasModal';
import { FormInput } from '../FormInput';
import { HeaderBrand } from '../HeaderBrand';
import { MenuButton } from '../MenuButton';
import { Placeholder } from '../Placeholder';
import { ScreenLoader } from '../ScreenLoader';
import { ScreenMessage } from '../ScreenMessage';
import { SelectField } from '../SelectField';
import { StatusBadge } from '../StatusBadge';
import { ThemeSelector } from '../ThemeSelector';

jest.mock('../../hooks/useNotifications', () => ({ useNotifications: () => ({ data: { items: [], unreadCount: 2 } }) }));
jest.mock('../../hooks/useRefunds', () => ({ useRefunds: () => ({ data: { items: [], pendingCount: 1 } }) }));
jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(),
  setItemAsync: jest.fn(),
  deleteItemAsync: jest.fn(),
}));

/**
 * Lists the colors of one palette that the other palette does not have, written as they appear in a style.
 * @param own Palette whose colors are listed.
 * @param other Palette that must not share them.
 * @returns The colors of `own` that `other` does not use.
 */
function colorsOnlyIn(own: Readonly<Record<string, string>>, other: Readonly<Record<string, string>>): readonly string[] {
  const otherColors = new Set(Object.values(other).map((color) => color.toLowerCase()));
  return [...new Set(Object.values(own).map((color) => color.toLowerCase()))].filter((color) => !otherColors.has(color));
}

const LIGHT_ONLY = colorsOnlyIn(LIGHT_PALETTE as unknown as Record<string, string>, DARK_PALETTE as unknown as Record<string, string>);
const DARK_ONLY = colorsOnlyIn(DARK_PALETTE as unknown as Record<string, string>, LIGHT_PALETTE as unknown as Record<string, string>);

const booking = parseBooking(bookingPayload);

/** Everything the base components and the cards show, each one as the app uses it. */
const SCENES: Readonly<Record<string, () => ReactElement>> = {
  header: () => <HeaderBrand />,
  statuses: () => (
    <>
      {BOOKING_STATUSES.map((status) => (
        <StatusBadge key={status} status={status} />
      ))}
    </>
  ),
  buttons: () => (
    <>
      <ActionButton label="Guardar" onPress={jest.fn()} />
      <ActionButton label="Cancelar reserva" onPress={jest.fn()} variant="danger" />
      <ActionButton label="No disponible" onPress={jest.fn()} disabled />
    </>
  ),
  card: () => (
    <AppCard>
      <AmountRow label="Saldo" money={{ amount: 50000, currency: 'CRC' }} emphasized />
      <AmountRow label="Pagado" money={{ amount: 10000, currency: 'CRC' }} />
    </AppCard>
  ),
  fields: () => (
    <>
      <FormInput label="Cliente" value="María" onChangeText={jest.fn()} />
      <FormInput label="Correo" value="" onChangeText={jest.fn()} error="Escribe un correo válido." />
      <SelectField label="Paquete" options={['Básico', 'Estándar']} value="Básico" onChange={jest.fn()} placeholder="Selecciona" />
      <DurationPicker label="Duración" options={[1, 2, 3]} value={2} onChange={jest.fn()} />
    </>
  ),
  states: () => (
    <>
      <ScreenLoader accessibilityLabel="Cargando" />
      <ScreenMessage message="No se pudo cargar." />
      <Placeholder message="Sin conexión." onRetry={jest.fn()} />
    </>
  ),
  contract: () => <ContractCard booking={booking} />,
  extras: () => (
    <>
      <ExtraRow extra={{ code: 'makeup', label: 'Maquillaje', unitPrice: 20000 }} quantity={1} onChange={jest.fn()} />
      <ExtrasModal selected={[]} onConfirm={jest.fn()} onClose={jest.fn()} />
    </>
  ),
  selector: () => <ThemeSelector />,
  menu: () => (
    <>
      <MenuButton />
      <AppMenu />
    </>
  ),
};

/**
 * Renders a scene in a theme, inside the theme provider like the app does.
 * @param scene Name of the scene.
 * @param scheme Theme to render it in.
 * @returns Everything the scene painted, as text, in lower case.
 */
function paint(scene: string, scheme: ColorScheme): string {
  useAppearanceStore.setState({ preference: scheme });
  // The menu is drawn only for a signed-in photographer, and it is the scene that opens it.
  useSessionStore.setState({
    status: 'signedIn',
    session: {
      accessToken: 't',
      accessTokenExpiresAt: 1,
      photographerId: 'p',
      username: 'u',
      email: 'u@example.com',
      name: 'Guillermo Ramírez',
      isSystemOperator: false,
      emailVerified: true,
    },
  });
  useMenuStore.setState({ open: scene === 'menu' });
  const build = SCENES[scene];
  if (build === undefined) {
    throw new Error(`unknown scene ${scene}`);
  }

  const { unmount } = render(<AppThemeProvider>{build()}</AppThemeProvider>);
  const painted = JSON.stringify(screen.toJSON()).toLowerCase();
  unmount();
  return painted;
}

describe('every screen follows the theme', () => {
  it('has colors that tell the two themes apart', () => {
    expect(LIGHT_ONLY.length).toBeGreaterThan(8);
    expect(DARK_ONLY.length).toBeGreaterThan(8);
  });

  describe.each(Object.keys(SCENES))('%s', (scene) => {
    it('paints nothing with a color of the dark theme while the theme is light', () => {
      const painted = paint(scene, 'light');

      expect(DARK_ONLY.filter((color) => painted.includes(color))).toEqual([]);
    });

    it('paints nothing with a color of the light theme while the theme is dark', () => {
      const painted = paint(scene, 'dark');

      expect(LIGHT_ONLY.filter((color) => painted.includes(color))).toEqual([]);
    });
  });

  it('really changes the colors when the theme changes: the same card is painted differently', () => {
    expect(paint('card', 'light')).not.toBe(paint('card', 'dark'));
  });

  it('can tell: each theme does paint with colors of its own, so finding none of the other theme means something', () => {
    const light = paint('fields', 'light');
    const dark = paint('fields', 'dark');

    expect(LIGHT_ONLY.some((color) => light.includes(color))).toBe(true);
    expect(DARK_ONLY.some((color) => dark.includes(color))).toBe(true);
  });
});

describe('ThemeSelector', () => {
  beforeEach(() => {
    useAppearanceStore.setState({ preference: 'light' });
  });

  it('offers the three choices and shows the one in use', () => {
    render(<ThemeSelector />);

    expect(screen.getByText('Apariencia')).toBeTruthy();
    expect(screen.getByLabelText('Apariencia: Sistema')).toBeTruthy();
    expect(screen.getByLabelText('Apariencia: Claro')).toBeTruthy();
    expect(screen.getByLabelText('Apariencia: Oscuro')).toBeTruthy();
    expect(screen.getByLabelText('Apariencia: Claro').props.accessibilityState).toMatchObject({ checked: true });
  });

  it.each([
    ['Oscuro', 'dark'],
    ['Sistema', 'system'],
  ] as const)('applies %s as soon as it is tapped', (label, expected) => {
    render(<ThemeSelector />);

    fireEvent.press(screen.getByLabelText(`Apariencia: ${label}`));

    expect(useAppearanceStore.getState().preference).toBe(expected);
  });

  it('shows the choice that was made', () => {
    render(<ThemeSelector />);
    fireEvent.press(screen.getByLabelText('Apariencia: Oscuro'));

    expect(screen.getByLabelText('Apariencia: Oscuro').props.accessibilityState).toMatchObject({ checked: true });
    expect(screen.getByLabelText('Apariencia: Claro').props.accessibilityState).toMatchObject({ checked: false });
  });
});
