import { ImageManipulator, SaveFormat } from 'expo-image-manipulator';
import { Image } from 'react-native';
import { ImageFormat, Skia, type SkImage } from '@shopify/react-native-skia';
import {
  PROOF_INTERMEDIATE_QUALITY,
  PROOF_LONG_SIDE,
  WATERMARK_OPACITY,
  WATERMARK_SHADOW_OPACITY,
  WATERMARK_SHADOW_RATIO,
  WATERMARK_SIZE_RATIO,
} from '../constants/upload';
import type { ProofEngine, ProofSource } from '../types/media/upload.types';
import type { ImageSize } from '../types/utils/upload.types';
import { fitWithin, placeWatermark } from '../utils/proofImage';

/**
 * Creates the engine that draws proofs on the phone. The photo is first reduced by the native image library (which decodes it turned
 * the way the camera had it and is the cheapest way to shrink a photo of many megapixels) and then drawn with Skia, which puts the
 * logo of the app across the middle and encodes the JPEG. The JPEG that Skia writes carries no metadata: no location, no camera, no date.
 * It is the same specification as the web of the photographer (`proofRenderer.ts` of the portal): a big logo, 70 % of the shorter
 * side, 60 % opaque, with a soft shadow.
 * @param watermarkSource The bundled image of the logo (the result of `require`).
 * @param discard Deletes a temporary file of the cache.
 * @returns The engine.
 */
export function createSkiaProofEngine(watermarkSource: number, discard: (uri: string) => void): ProofEngine {
  let watermark: Promise<SkImage> | null = null;

  /**
   * Loads the logo the first time and gives the same one afterwards.
   * @returns The logo.
   */
  function getWatermark(): Promise<SkImage> {
    watermark ??= Skia.Data.fromURI(Image.resolveAssetSource(watermarkSource).uri).then((data) => {
      const image = Skia.Image.MakeImageFromEncoded(data);
      if (image === null) {
        throw new Error('The watermark could not be decoded.');
      }

      return image;
    });
    return watermark;
  }

  /**
   * Draws the photo and the logo at a size and encodes them.
   * @param photo The photo, already reduced.
   * @param mark The logo.
   * @param size Size of the proof.
   * @param quality JPEG quality from 0 to 100.
   * @returns The bytes, or null when the surface could not be made.
   */
  function draw(photo: SkImage, mark: SkImage, size: ImageSize, quality: number): Uint8Array | null {
    const surface = Skia.Surface.Make(size.width, size.height);
    if (surface === null) {
      return null;
    }

    try {
      const canvas = surface.getCanvas();
      canvas.drawImageRect(photo, Skia.XYWHRect(0, 0, photo.width(), photo.height()), Skia.XYWHRect(0, 0, size.width, size.height), Skia.Paint(), true);

      const place = placeWatermark(size, { width: mark.width(), height: mark.height() }, WATERMARK_SIZE_RATIO);
      const paint = Skia.Paint();
      paint.setAlphaf(WATERMARK_OPACITY);
      // Canvas shadow blur is twice the sigma of a gaussian blur.
      const sigma = (Math.min(size.width, size.height) * WATERMARK_SHADOW_RATIO) / 2;
      paint.setImageFilter(Skia.ImageFilter.MakeDropShadow(0, 0, sigma, sigma, Skia.Color(`rgba(0, 0, 0, ${WATERMARK_SHADOW_OPACITY})`), null));
      canvas.drawImageRect(mark, Skia.XYWHRect(0, 0, mark.width(), mark.height()), Skia.XYWHRect(place.x, place.y, place.width, place.height), paint, true);

      surface.flush();
      const snapshot = surface.makeImageSnapshot();
      try {
        return snapshot.encodeToBytes(ImageFormat.JPEG, quality);
      } finally {
        snapshot.dispose();
      }
    } finally {
      surface.dispose();
    }
  }

  return {
    async open(uri): Promise<ProofSource> {
      const full = await ImageManipulator.manipulate(uri).renderAsync();
      let intermediate: string;
      try {
        const target = fitWithin({ width: full.width, height: full.height }, PROOF_LONG_SIDE);
        const reduced = target.width === full.width && target.height === full.height ? full : await ImageManipulator.manipulate(full).resize(target).renderAsync();
        try {
          intermediate = (await reduced.saveAsync({ format: SaveFormat.JPEG, compress: PROOF_INTERMEDIATE_QUALITY })).uri;
        } finally {
          if (reduced !== full) {
            reduced.release();
          }
        }
      } finally {
        full.release();
      }

      let photo: SkImage | null;
      try {
        photo = Skia.Image.MakeImageFromEncoded(await Skia.Data.fromURI(intermediate));
      } finally {
        discard(intermediate);
      }

      if (photo === null) {
        throw new Error('The photo could not be decoded.');
      }

      const mark = await getWatermark();
      const opened = photo;
      return {
        width: opened.width(),
        height: opened.height(),
        encode: (size, quality) => Promise.resolve(draw(opened, mark, size, quality)),
        close: () => opened.dispose(),
      };
    },
  };
}
