import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useFolderDownload } from '../hooks/useFolderDownload';
import type { StudioGallery } from '../types/api/studio';
import type { FolderDownload } from '../types/hooks/useFolderDownload.types';
import { ArchiveGalleryCard } from './ArchiveGalleryCard';

vi.mock('../hooks/useFolderDownload');

const gallery = {
  id: '0197a000-0000-7000-8000-000000000001',
  clientName: 'María Pérez',
  packageName: 'Retrato Familiar',
  deliveredAt: '2026-10-25T12:00:00+00:00',
  allowedActions: ['UploadZip', 'Release'],
} as unknown as StudioGallery;

/**
 * Makes the hook of the saving answer a state.
 * @param overrides What differs from "nothing started yet".
 * @returns The start function the card calls.
 */
function savingIs(overrides: Partial<FolderDownload> = {}): ReturnType<typeof vi.fn> {
  const start = vi.fn();
  vi.mocked(useFolderDownload).mockReturnValue({ phase: 'idle', handled: 0, total: 0, saved: 0, failed: [], problem: null, canUseFolder: true, start, ...overrides });
  return start;
}

/**
 * Draws the card.
 */
function renderCard(): void {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <ArchiveGalleryCard gallery={gallery} />
    </QueryClientProvider>,
  );
}

describe('the card of a gallery to archive', () => {
  beforeEach(() => vi.clearAllMocks());

  it('does not offer to free the space before the photos are saved, and starts the saving on request', async () => {
    const start = savingIs();
    renderCard();

    expect(screen.getByText('María Pérez')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Liberar espacio' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Descargar a una carpeta' }));
    expect(start).toHaveBeenCalledTimes(1);
  });

  it('offers it once the photos were saved and checked', () => {
    savingIs({ phase: 'done', total: 12, saved: 12, handled: 12 });
    renderCard();

    expect(screen.getByText(/12 fotos guardadas y comprobadas/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Liberar espacio' })).toBeInTheDocument();
  });

  it('keeps it closed while some photo failed, and says which', () => {
    savingIs({ phase: 'problem', total: 3, saved: 2, handled: 3, failed: [{ photoId: 'b', name: 'IMG_0002.CR2', reason: 'size' }] });
    renderCard();

    expect(screen.getByText(/IMG_0002.CR2 \(llegó con otro tamaño\)/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Liberar espacio' })).not.toBeInTheDocument();
  });

  it('offers it when the photographer says they already have the photos', async () => {
    savingIs();
    renderCard();

    await userEvent.click(screen.getByRole('checkbox', { name: /Ya tengo estas fotos respaldadas/ }));

    expect(screen.getByRole('button', { name: 'Liberar espacio' })).toBeInTheDocument();
  });

  it('says it cannot check the files in a browser without folders', () => {
    savingIs({ canUseFolder: false });
    renderCard();

    expect(screen.getByRole('button', { name: 'Descargar los finales' })).toBeInTheDocument();
    expect(screen.getByText(/no puede comprobar que quedaron completos/)).toBeInTheDocument();
  });
});
