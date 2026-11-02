import { act, renderHook, waitFor } from '@testing-library/react-native';
import { Alert, type AlertButton } from 'react-native';
import { galleryApi } from '../../api/client';
import { parseGallery } from '../../api/galleryApi';
import { galleryPayload } from '../../api/__fixtures__/galleryPayloads';
import { createQueryWrapper } from '../__fixtures__/queryWrapper';
import { useGalleryActions } from '../useGalleryActions';

jest.mock('../../api/client', () => ({
  galleryApi: {
    openSelection: jest.fn(),
    publishFinals: jest.fn(),
    releaseExtras: jest.fn(),
    removeZip: jest.fn(),
    removeProof: jest.fn(),
  },
}));

const gallery = parseGallery(galleryPayload);

/**
 * Taps a button of the last dialog.
 * @param text The label of the button.
 */
function tap(text: string): void {
  const buttons: readonly AlertButton[] = jest.mocked(Alert.alert).mock.calls.at(-1)?.[2] ?? [];
  buttons.find((button) => button.text === text)?.onPress?.();
}

describe('useGalleryActions', () => {
  const { client, Wrapper } = createQueryWrapper();

  beforeEach(() => {
    jest.clearAllMocks();
    jest.spyOn(Alert, 'alert').mockImplementation(() => undefined);
  });

  afterEach(() => {
    client.clear();
  });

  it.each([
    ['openSelection', 'Publicar las pruebas', 'Publicar', 'openSelection'],
    ['publishFinals', 'Publicar los finales del paquete', 'Publicar finales', 'publishFinals'],
    ['releaseExtras', 'Entregar las adicionales', 'Sí, ya pagó', 'releaseExtras'],
    ['removeZip', 'Borrar el ZIP', 'Borrar el ZIP', 'removeZip'],
  ] as const)('%s asks first, and only runs after the photographer agrees', async (action, title, confirm, call) => {
    jest.mocked(galleryApi[call]).mockResolvedValue(gallery);
    const { result } = renderHook(() => useGalleryActions(gallery), { wrapper: Wrapper });

    act(() => result.current[action]());
    expect(Alert.alert).toHaveBeenCalledWith(title, expect.any(String), expect.any(Array));
    expect(galleryApi[call]).not.toHaveBeenCalled();
    act(() => tap(confirm));

    await waitFor(() => expect(galleryApi[call]).toHaveBeenCalledWith(gallery.id));
  });

  it('asks before removing a proof, and removes that one', async () => {
    jest.mocked(galleryApi.removeProof).mockResolvedValue(gallery);
    const { result } = renderHook(() => useGalleryActions(gallery), { wrapper: Wrapper });

    act(() => result.current.removeProof('p1'));
    expect(galleryApi.removeProof).not.toHaveBeenCalled();
    act(() => tap('Quitar'));

    await waitFor(() => expect(galleryApi.removeProof).toHaveBeenCalledWith(gallery.id, 'p1'));
  });

  it('does nothing when the photographer goes back', () => {
    const { result } = renderHook(() => useGalleryActions(gallery), { wrapper: Wrapper });

    act(() => result.current.publishFinals());
    act(() => tap('Volver'));

    expect(galleryApi.publishFinals).not.toHaveBeenCalled();
  });

  it('says in words why a step failed', async () => {
    const { ApiError } = jest.requireActual('../../api/httpClient');
    jest.mocked(galleryApi.publishFinals).mockRejectedValue(new ApiError(409, 'x', 'gallery.finals_incomplete'));
    const { result } = renderHook(() => useGalleryActions(gallery), { wrapper: Wrapper });

    act(() => result.current.publishFinals());
    act(() => tap('Publicar finales'));

    await waitFor(() => expect(result.current.errorMessage).toContain('Faltan finales'));
  });
});
