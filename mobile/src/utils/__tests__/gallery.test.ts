import { parseGallery, parseSelection } from '../../api/galleryApi';
import { galleryPayload, selectionPayload } from '../../api/__fixtures__/galleryPayloads';
import { describeFinal, describeGalleryStatus, describeTerms, describeZip, namesOf, splitSelection, validateReopenReason, validateTerms } from '../gallery';

const gallery = parseGallery(galleryPayload);
const selection = parseSelection(selectionPayload);

describe('describeGalleryStatus', () => {
  it('says each state in words, with what to do next and a color', () => {
    expect(describeGalleryStatus('InEditing')).toMatchObject({ label: 'En edición', tone: 'info' });
    expect(describeGalleryStatus('Delivered').tone).toBe('success');
    expect(describeGalleryStatus('Pending').hint).toContain('Sube las pruebas');
    expect(describeGalleryStatus('Archived').tone).toBe('muted');
  });
});

describe('describeTerms', () => {
  it('writes what the package includes and what an extra costs, and the gift only when there is one', () => {
    expect(describeTerms(gallery)).toMatch(/^2 fotos incluidas · .*3.*000.* cada foto extra$/);
    expect(describeTerms({ ...gallery, includedPhotos: 30, bonusPhotos: 5 })).toMatch(/^30 fotos incluidas · 5 de regalo · /);
    expect(describeTerms({ ...gallery, includedPhotos: 1 })).toMatch(/^1 foto incluida · /);
  });
});

describe('describeFinal', () => {
  it('says the size in pixels and in megabytes, when it was taken and how many times it was downloaded', () => {
    expect(describeFinal(selection.photos[0]!)).toMatch(/^final subido · 1250 × 3000 px · 4,2 MB · tomada el .*17 de octubre de 2026 · descargada 2 veces$/);
    expect(describeFinal({ ...selection.photos[0]!, finalDownloads: 1, finalWidth: null, finalCapturedAt: null })).toBe('final subido · 4,2 MB · descargada 1 vez');
    expect(describeFinal({ ...selection.photos[0]!, finalDownloads: 0, finalWidth: null, finalCapturedAt: null })).toBe('final subido · 4,2 MB · sin descargas');
  });

  it('says there is no final yet', () => {
    expect(describeFinal(selection.photos[1]!)).toBe('sin final');
  });
});

describe('splitSelection and namesOf', () => {
  it('splits the photos of the package from the extra ones, and joins the names one per line', () => {
    const { packagePhotos, extraPhotos } = splitSelection(selection);

    expect(packagePhotos.map((photo) => photo.name)).toEqual(['IMG_0001.CR2', 'IMG_0002.CR2']);
    expect(extraPhotos.map((photo) => photo.name)).toEqual(['IMG_0003.CR2']);
    expect(namesOf(packagePhotos)).toBe('IMG_0001.CR2\nIMG_0002.CR2');
    expect(namesOf([])).toBe('');
  });
});

describe('describeZip', () => {
  it('says its size and whether the client downloaded it', () => {
    const base = { bytes: 220_000_000, uploadedAt: '2026-11-03T14:00:00+00:00' };

    expect(describeZip({ ...base, downloads: 0, lastDownloadedAt: null })[0]).toContain('209,8 MB');
    expect(describeZip({ ...base, downloads: 0, lastDownloadedAt: null })[1]).toBe('El cliente todavía no lo ha descargado.');
    expect(describeZip({ ...base, downloads: 2, lastDownloadedAt: '2026-11-04T15:00:00+00:00' })[1]).toMatch(/^El cliente lo ha descargado 2 veces \(la última, el /);
    expect(describeZip({ ...base, downloads: 1, lastDownloadedAt: null })[1]).toBe('El cliente lo ha descargado 1 vez.');
  });
});

describe('validateTerms', () => {
  it('reads the numbers as typed, and treats an empty gift as none', () => {
    expect(validateTerms('30', '5', '3000', 150)).toEqual({ ok: true, includedPhotos: 30, bonusPhotos: 5, extraPhotoPrice: 3000 });
    expect(validateTerms(' 30 ', '', '₡ 3.000'.replace('₡', ''), 150)).toEqual({ ok: true, includedPhotos: 30, bonusPhotos: 0, extraPhotoPrice: 3000 });
  });

  it('refuses what is not a number, a price that is not, and more photos than a gallery holds', () => {
    expect(validateTerms('', '0', '3000', 150).ok).toBe(false);
    expect(validateTerms('3.5', '0', '3000', 150).ok).toBe(false);
    expect(validateTerms('30', '-1', '3000', 150).ok).toBe(false);
    expect(validateTerms('30', '0', 'abc', 150).ok).toBe(false);
    expect(validateTerms('100', '60', '3000', 150)).toMatchObject({ ok: false, error: expect.stringContaining('150') });
  });
});

describe('validateReopenReason', () => {
  it('needs a reason, without spaces around it and not too long', () => {
    expect(validateReopenReason('  cambio de fotos ', 500)).toEqual({ ok: true, reason: 'cambio de fotos' });
    expect(validateReopenReason('   ', 500).ok).toBe(false);
    expect(validateReopenReason('x'.repeat(501), 500)).toMatchObject({ ok: false });
    expect(validateReopenReason('x'.repeat(500), 500).ok).toBe(true);
  });
});
