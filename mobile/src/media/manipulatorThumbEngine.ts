import { File } from 'expo-file-system';
import { ImageManipulator, SaveFormat } from 'expo-image-manipulator';
import type { ThumbEngine } from '../types/media/upload.types';
import { fitWithin } from '../utils/proofImage';

/**
 * Creates the engine that makes the thumbnails of the finals with the native image library: it decodes the photo turned the way the
 * camera had it, reduces it and saves a JPEG in the cache. Nothing is drawn on it and no metadata is copied.
 * @param discard Deletes a temporary file of the cache.
 * @returns The engine.
 */
export function createManipulatorThumbEngine(discard: (uri: string) => void): ThumbEngine {
  return {
    async make(uri, longSide, quality) {
      const full = await ImageManipulator.manipulate(uri).renderAsync();
      try {
        const target = fitWithin({ width: full.width, height: full.height }, longSide);
        const reduced = target.width === full.width && target.height === full.height ? full : await ImageManipulator.manipulate(full).resize(target).renderAsync();
        try {
          const saved = await reduced.saveAsync({ format: SaveFormat.JPEG, compress: quality });
          return { uri: saved.uri, bytes: new File(saved.uri).size };
        } finally {
          if (reduced !== full) {
            reduced.release();
          }
        }
      } finally {
        full.release();
      }
    },
    discard,
  };
}
