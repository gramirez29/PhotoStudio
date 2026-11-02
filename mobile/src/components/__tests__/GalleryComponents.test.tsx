import { fireEvent, render, screen } from '@testing-library/react-native';
import { Image } from 'react-native';
import { parseBooking } from '../../api/bookingsApi';
import { parseGallery, parseSelection } from '../../api/galleryApi';
import { bookingPayload } from '../../api/__fixtures__/testResponses';
import { galleryPayload, selectionPayload } from '../../api/__fixtures__/galleryPayloads';
import { useDownloadFinal } from '../../hooks/useDownloadFinal';
import { useFinalView, useGalleryOfBooking, useGallerySelection } from '../../hooks/useGallery';
import { openGallery, openGalleryReopen, openGalleryTerms } from '../../navigation/appNavigation';
import { shareText } from '../../navigation/externalLinks';
import type { GalleryActionHandlers } from '../../types/hooks/useGalleryActions.types';
import { GalleryActions } from '../GalleryActions';
import { GalleryCard } from '../GalleryCard';
import { GalleryPhotoTile } from '../GalleryPhotoTile';
import { GalleryPhotoViewer } from '../GalleryPhotoViewer';
import { GalleryProofGrid } from '../GalleryProofGrid';
import { GallerySelection } from '../GallerySelection';
import { GalleryZipCard } from '../GalleryZipCard';

jest.mock('../../media/deviceUploaders', () => ({
  deviceProofUploader: { run: jest.fn() },
  deviceFinalUploader: { run: jest.fn() },
  deviceZipUploader: { run: jest.fn() },
  discardPickedFile: jest.fn(),
  makeProofSample: jest.fn(),
}));
jest.mock('../GalleryFinalUpload', () => ({ GalleryFinalUpload: () => null }));
jest.mock('../GalleryZipUpload', () => ({ GalleryZipUpload: () => null }));
jest.mock('../GalleryProofUpload', () => ({ GalleryProofUpload: () => null }));
jest.mock('../../media/devicePickers', () => ({ pickPhotos: jest.fn(), pickZip: jest.fn() }));
jest.mock('../../hooks/useGallery');
jest.mock('../../hooks/useDownloadFinal');
jest.mock('../../navigation/appNavigation');
jest.mock('../../navigation/externalLinks');

const gallery = parseGallery(galleryPayload);
const selection = parseSelection(selectionPayload);
const booking = { ...parseBooking(bookingPayload), status: 'Completed' as const };

/**
 * Builds the handlers of the actions, all mocked.
 * @param overrides Values to replace.
 * @returns The handlers.
 */
function handlers(overrides: Partial<GalleryActionHandlers> = {}): GalleryActionHandlers {
  return { isPending: false, errorMessage: null, openSelection: jest.fn(), publishFinals: jest.fn(), releaseExtras: jest.fn(), removeZip: jest.fn(), removeProof: jest.fn(), ...overrides };
}

/**
 * Makes the query of the selection return a value.
 * @param data The selection.
 */
function selectionIs(data: typeof selection | undefined): void {
  jest.mocked(useGallerySelection).mockReturnValue({ data, isPending: data === undefined, isError: false } as ReturnType<typeof useGallerySelection>);
}

beforeEach(() => {
  jest.clearAllMocks();
  jest.mocked(useDownloadFinal).mockReturnValue({ mutate: jest.fn(), isPending: false, isError: false } as unknown as ReturnType<typeof useDownloadFinal>);
  jest.mocked(useFinalView).mockReturnValue({ data: undefined, isPending: true, isError: false } as ReturnType<typeof useFinalView>);
});

describe('GalleryCard', () => {
  it('shows nothing for a booking without a gallery, and asks only when the session was completed', () => {
    jest.mocked(useGalleryOfBooking).mockReturnValue({ data: null } as ReturnType<typeof useGalleryOfBooking>);

    render(<GalleryCard booking={{ ...booking, status: 'Confirmed' }} />);

    expect(screen.queryByText('Galería de fotos')).toBeNull();
    expect(useGalleryOfBooking).toHaveBeenCalledWith(booking.id, false);
  });

  it('shows where the gallery is, what the client chose, and opens it', () => {
    jest.mocked(useGalleryOfBooking).mockReturnValue({ data: gallery } as ReturnType<typeof useGalleryOfBooking>);

    render(<GalleryCard booking={booking} />);

    expect(screen.getByText('En edición')).toBeTruthy();
    expect(screen.getByText('El cliente eligió 3 fotos (1 adicional).')).toBeTruthy();
    fireEvent.press(screen.getByRole('button', { name: 'Abrir la galería' }));
    expect(openGallery).toHaveBeenCalledWith(gallery.id);
  });
});

describe('GalleryActions', () => {
  it('shows only what the API allows, and each step asks through its handler', () => {
    const run = handlers();
    render(<GalleryActions gallery={{ ...gallery, allowedActions: ['EditTerms', 'OpenSelection', 'PublishFinals', 'ReleaseExtras', 'ReopenSelection'] }} handlers={run} />);

    fireEvent.press(screen.getByRole('button', { name: 'Publicar las pruebas' }));
    fireEvent.press(screen.getByRole('button', { name: 'Publicar los finales del paquete' }));
    fireEvent.press(screen.getByRole('button', { name: 'Entregar las adicionales' }));
    fireEvent.press(screen.getByRole('button', { name: 'Fotos incluidas, de regalo y precio' }));
    fireEvent.press(screen.getByRole('button', { name: 'Reabrir la selección' }));

    expect(run.openSelection).toHaveBeenCalled();
    expect(run.publishFinals).toHaveBeenCalled();
    expect(run.releaseExtras).toHaveBeenCalled();
    expect(openGalleryTerms).toHaveBeenCalledWith(gallery.id);
    expect(openGalleryReopen).toHaveBeenCalledWith(gallery.id);
  });

  it('offers nothing that is not allowed, and shows nothing at all when there is nothing to do', () => {
    const { toJSON } = render(<GalleryActions gallery={{ ...gallery, allowedActions: [] }} handlers={handlers()} />);

    expect(toJSON()).toBeNull();
  });

  it('shows why a step failed', () => {
    render(<GalleryActions gallery={{ ...gallery, allowedActions: ['PublishFinals'] }} handlers={handlers({ errorMessage: 'Faltan finales' })} />);

    expect(screen.getByText('Faltan finales')).toBeTruthy();
  });
});

describe('GallerySelection', () => {
  it('says the client has not sent their selection yet', () => {
    selectionIs(undefined);

    render(<GallerySelection gallery={{ ...gallery, selectionSubmittedAt: null }} />);

    expect(screen.getByText('El cliente todavía no ha enviado su selección.')).toBeTruthy();
    expect(useGallerySelection).toHaveBeenCalledWith(gallery.id, false);
  });

  it('shows the photos of the package and the extra ones apart, each with its small image and its ficha', () => {
    selectionIs(selection);

    render(<GallerySelection gallery={gallery} />);

    expect(screen.getByText('Fotos del paquete (2)')).toBeTruthy();
    expect(screen.getByText('Fotos adicionales (1)')).toBeTruthy();
    expect(screen.getByText('1 de 2 con su final')).toBeTruthy();
    expect(screen.getByText('1250 x 3000 px')).toBeTruthy();
    expect(screen.getByText('Tamaño: 4.2 MB')).toBeTruthy();
    expect(screen.getByText('Tomada: 17/10/2026')).toBeTruthy();
    expect(screen.getByText('Descargas hechas: 2')).toBeTruthy();
    expect(screen.getAllByText('Descargas hechas: -')).toHaveLength(2);
    expect(screen.getByRole('button', { name: 'Ver IMG_0001.CR2' })).toBeTruthy();
    expect(screen.getAllByRole('button', { name: /Descargar/ })).toHaveLength(1);
  });

  it('copies or shares the names of the files of one block, one per line', () => {
    selectionIs(selection);
    render(<GallerySelection gallery={gallery} />);

    fireEvent.press(screen.getAllByRole('button', { name: 'Copiar o compartir los nombres' })[1]!);

    expect(shareText).toHaveBeenCalledWith('IMG_0003.CR2');
  });

  it('opens a photo large, in its real quality, and moves to the next', () => {
    selectionIs(selection);
    jest.mocked(useFinalView).mockReturnValue({ data: 'https://storage.test/finals/p1.jpg?sig=secret', isPending: false, isError: false } as ReturnType<typeof useFinalView>);
    render(<GallerySelection gallery={gallery} />);

    fireEvent.press(screen.getByRole('button', { name: 'Ver IMG_0001.CR2' }));

    expect(screen.getByLabelText('IMG_0001.CR2').props.source).toEqual({ uri: 'https://storage.test/finals/p1.jpg?sig=secret' });
    expect(useFinalView).toHaveBeenCalledWith(gallery.id, 'p1', true);
    fireEvent.press(screen.getByRole('button', { name: 'Siguiente' }));
    expect(useFinalView).toHaveBeenLastCalledWith(gallery.id, 'p2', false);
    fireEvent.press(screen.getByRole('button', { name: 'Cerrar' }));
    expect(screen.queryByRole('button', { name: 'Siguiente' })).toBeNull();
  });
});

describe('GalleryPhotoTile', () => {
  const photo = selection.photos[0]!;

  it('has the download icon inside the card, named by what it does, and runs it', () => {
    const onDownload = jest.fn();
    render(<GalleryPhotoTile photo={photo} onOpen={jest.fn()} onDownload={onDownload} downloading={false} />);

    fireEvent.press(screen.getByRole('button', { name: 'Descargar el final de IMG_0001.CR2' }));

    expect(onDownload).toHaveBeenCalledTimes(1);
    expect(screen.getByText('Final')).toBeTruthy();
    expect(screen.getByText('IMG_0001.CR2')).toBeTruthy();
  });

  it('does not offer to download a photo that has no final, and says so on the card', () => {
    render(<GalleryPhotoTile photo={selection.photos[1]!} onOpen={jest.fn()} onDownload={jest.fn()} downloading={false} />);

    expect(screen.queryByRole('button', { name: /Descargar/ })).toBeNull();
    expect(screen.getByText('Sin final')).toBeTruthy();
  });

  it('cannot be pressed again while the download is being prepared', () => {
    const onDownload = jest.fn();
    render(<GalleryPhotoTile photo={photo} onOpen={jest.fn()} onDownload={onDownload} downloading />);

    fireEvent.press(screen.getByRole('button', { name: 'Descargar el final de IMG_0001.CR2' }));

    expect(onDownload).not.toHaveBeenCalled();
  });

  it('shows an icon, not an empty gap, when the photo has no image or its image cannot be loaded', () => {
    const { rerender } = render(<GalleryPhotoTile photo={{ ...photo, thumbUrl: null }} onOpen={jest.fn()} onDownload={jest.fn()} downloading={false} />);
    expect(screen.UNSAFE_queryByType(Image)).toBeNull();

    rerender(<GalleryPhotoTile photo={photo} onOpen={jest.fn()} onDownload={jest.fn()} downloading={false} />);
    fireEvent(screen.UNSAFE_getByType(Image), 'error');

    expect(screen.UNSAFE_queryByType(Image)).toBeNull();
  });
});

describe('GalleryPhotoViewer', () => {
  it('shows the proof, which is what there is, for a photo without a final, and does not ask for the real one', () => {
    render(<GalleryPhotoViewer galleryId={gallery.id} photos={selection.photos} index={1} onMove={jest.fn()} onClose={jest.fn()} />);

    expect(screen.getByLabelText('IMG_0002.CR2').props.source).toEqual({ uri: 'https://storage.test/proofs/p2.jpg?sig=secret' });
    expect(screen.getByText(/prueba, todavía sin final/)).toBeTruthy();
    expect(useFinalView).toHaveBeenCalledWith(gallery.id, 'p2', false);
  });

  it('says so, and keeps the small image, when the real photo cannot be loaded', () => {
    jest.mocked(useFinalView).mockReturnValue({ data: undefined, isPending: false, isError: true } as ReturnType<typeof useFinalView>);

    render(<GalleryPhotoViewer galleryId={gallery.id} photos={selection.photos} index={0} onMove={jest.fn()} onClose={jest.fn()} />);

    expect(screen.getByText(/No pudimos cargar la foto en su calidad real/)).toBeTruthy();
    expect(screen.getByLabelText('IMG_0001.CR2').props.source).toEqual({ uri: 'https://storage.test/thumbs/p1.jpg?sig=secret' });
  });

  it('shows nothing when the position is not a photo', () => {
    const { toJSON } = render(<GalleryPhotoViewer galleryId={gallery.id} photos={selection.photos} index={9} onMove={jest.fn()} onClose={jest.fn()} />);

    expect(toJSON()).toBeNull();
  });
});

describe('GalleryProofGrid', () => {
  const proofs = [{ id: 'p1', name: 'IMG_0001.jpg', bytes: 10, url: 'https://storage.test/proofs/p1.jpg?sig=secret', expiresAt: 'x' }];

  it('shows the proofs and removes one only when removing is allowed, by pressing and holding', () => {
    const onRemove = jest.fn();
    const { rerender } = render(<GalleryProofGrid proofs={proofs} onRemove={onRemove} />);

    fireEvent(screen.getByLabelText('IMG_0001.jpg'), 'longPress');
    expect(onRemove).toHaveBeenCalledWith('p1');

    onRemove.mockClear();
    rerender(<GalleryProofGrid proofs={proofs} />);
    fireEvent(screen.getByLabelText('IMG_0001.jpg'), 'longPress');
    expect(onRemove).not.toHaveBeenCalled();
  });
});

describe('GalleryZipCard', () => {
  it('shows nothing without a ZIP', () => {
    const { toJSON } = render(<GalleryZipCard gallery={gallery} handlers={handlers()} />);

    expect(toJSON()).toBeNull();
  });

  it('shows its size and whether the client downloaded it, and deletes it through its handler', () => {
    const run = handlers();
    render(
      <GalleryZipCard
        gallery={{ ...gallery, allowedActions: ['RemoveZip'], zip: { bytes: 220_000_000, uploadedAt: '2026-11-03T14:00:00+00:00', downloads: 0, lastDownloadedAt: null } }}
        handlers={run}
      />,
    );

    expect(screen.getByText('El cliente todavía no lo ha descargado.')).toBeTruthy();
    fireEvent.press(screen.getByRole('button', { name: 'Borrar el ZIP' }));
    expect(run.removeZip).toHaveBeenCalled();
  });
});
