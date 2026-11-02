import { fireEvent, render, screen } from '@testing-library/react-native';
import { parseGallery, parseGalleryItems } from '../../api/galleryApi';
import { galleryPayload } from '../../api/__fixtures__/galleryPayloads';
import { useGalleryActions } from '../../hooks/useGalleryActions';
import { openGallery } from '../../navigation/appNavigation';
import { describeDeliveryAge } from '../../utils/gallery';
import { ArchivableGalleryItem } from '../ArchivableGalleryItem';

jest.mock('../../hooks/useGalleryActions');
jest.mock('../../navigation/appNavigation');

const NOW = Date.parse('2026-11-04T12:00:00Z');
const delivered = parseGallery({ ...galleryPayload, status: 'Delivered', deliveredAt: '2026-10-25T12:00:00+00:00' });

describe('galleries to archive', () => {
  it('reads the list the API sends', () => {
    const list = parseGalleryItems({ items: [galleryPayload, { ...galleryPayload, id: 'other' }], nextCursor: null });

    expect(list.map((gallery) => gallery.id)).toEqual([galleryPayload.id, 'other']);
    expect(() => parseGalleryItems({ nope: [] })).toThrow();
  });

  it('says how long ago a gallery was delivered', () => {
    expect(describeDeliveryAge(delivered, NOW)).toMatch(/^Entregada el .* · hace 10 días$/);
    expect(describeDeliveryAge({ ...delivered, deliveredAt: '2026-11-04T08:00:00+00:00' }, NOW)).toMatch(/hoy$/);
    expect(describeDeliveryAge({ ...delivered, deliveredAt: '2026-11-03T08:00:00+00:00' }, NOW)).toMatch(/hace 1 día$/);
    expect(describeDeliveryAge({ ...delivered, deliveredAt: null }, NOW)).toBe('Entregada');
  });

  it('shows the gallery, opens it, and frees its space only through the confirmation of the actions', () => {
    const release = jest.fn();
    jest.mocked(useGalleryActions).mockReturnValue({ isPending: false, errorMessage: null, release } as unknown as ReturnType<typeof useGalleryActions>);

    render(<ArchivableGalleryItem gallery={delivered} now={NOW} />);

    expect(screen.getByText('María Pérez')).toBeTruthy();
    expect(screen.getByText('Retrato Familiar')).toBeTruthy();
    fireEvent.press(screen.getByText('Abrir la galería'));
    expect(openGallery).toHaveBeenCalledWith(delivered.id);
    fireEvent.press(screen.getByText('Liberar espacio'));
    expect(release).toHaveBeenCalledTimes(1);
  });

  it('shows why releasing failed', () => {
    jest.mocked(useGalleryActions).mockReturnValue({ isPending: false, errorMessage: 'Algo falló', release: jest.fn() } as unknown as ReturnType<typeof useGalleryActions>);

    render(<ArchivableGalleryItem gallery={delivered} now={NOW} />);

    expect(screen.getByText('Algo falló')).toBeTruthy();
  });
});
