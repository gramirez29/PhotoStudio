/** A gallery as the API sends it: the client chose three photos, one of them extra, and the photographer is editing. */
export const galleryPayload = {
  id: '0197a000-0000-7000-8000-000000000001',
  bookingId: '0197a000-0000-7000-8000-0000000000b1',
  status: 'InEditing',
  clientName: 'María Pérez',
  packageName: 'Retrato Familiar',
  sessionStart: '2026-10-17T21:30:00+00:00',
  includedPhotos: 2,
  bonusPhotos: 0,
  selectablePhotos: 2,
  extraPhotoPrice: { amount: 3000, currency: 'CRC' },
  maxPhotos: 150,
  photoCount: 4,
  createdAt: '2026-10-17T23:45:00+00:00',
  allowedActions: ['UploadFinals', 'ReopenSelection'],
  selectedPhotoCount: 3,
  extraPhotoCount: 1,
  selectionSubmittedAt: '2026-10-30T12:00:00+00:00',
  finalsPublishedAt: null,
  deliveredAt: null,
  extrasReleasedAt: null,
  zip: null,
  selectionReopenedAt: null,
  selectionReopenReason: null,
} as const;

/** What the client chose: two photos of the package and one extra, the first with its final. */
export const selectionPayload = {
  submittedAt: '2026-10-30T12:00:00+00:00',
  extraPhotos: 1,
  extraTotal: { amount: 3000, currency: 'CRC' },
  photos: [
    {
      id: 'p1',
      name: 'IMG_0001.CR2',
      isExtra: false,
      finalBytes: 4_404_019,
      finalWidth: 1250,
      finalHeight: 3000,
      finalCapturedAt: '2026-10-17T21:30:00+00:00',
      finalDownloads: 2,
      thumbUrl: 'https://storage.test/thumbs/p1.jpg?sig=secret',
      thumbIsFinal: true,
    },
    { id: 'p2', name: 'IMG_0002.CR2', isExtra: false, finalBytes: null, thumbUrl: 'https://storage.test/proofs/p2.jpg?sig=secret', thumbIsFinal: false },
    { id: 'p3', name: 'IMG_0003.CR2', isExtra: true, finalBytes: null, thumbUrl: 'https://storage.test/proofs/p3.jpg?sig=secret', thumbIsFinal: false },
  ],
} as const;
