import { describe, expect, it, vi } from 'vitest';
import { THUMB_LONG_SIDE, THUMB_MAX_BYTES } from '../constants/finals';
import type { ProofCanvas } from '../types/media/proofRenderer.types';
import { createThumbRenderer } from './thumbRenderer';

/**
 * Builds the pieces of the browser the renderer needs, with canvases that give a blob of the size each quality is told to weigh.
 * @param weights For each canvas drawn (each size tried), the size of the blob at each quality, from the first quality tried to the last.
 * @returns The dependencies, and what was drawn.
 */
function browser(weights: readonly (readonly number[])[]): {
  readonly deps: Parameters<typeof createThumbRenderer>[0];
  readonly sizes: { width: number; height: number }[];
  readonly closed: { count: number };
} {
  const sizes: { width: number; height: number }[] = [];
  const closed = { count: 0 };
  const image = { width: 6000, height: 4000, close: () => void (closed.count += 1) } as unknown as ImageBitmap;
  return {
    sizes,
    closed,
    deps: {
      decode: () => Promise.resolve(image),
      createCanvas: (size): ProofCanvas => {
        sizes.push(size);
        const mine = weights[Math.min(sizes.length - 1, weights.length - 1)] ?? [1];
        let call = 0;
        return {
          getContext: () => ({ drawImage: vi.fn() }) as unknown as CanvasRenderingContext2D,
          toJpeg: () => Promise.resolve(new Blob([new Uint8Array(mine[call++ % mine.length] ?? 1)], { type: 'image/jpeg' })),
        };
      },
    },
  };
}

describe('the thumbnail of a final', () => {
  it('is the photo reduced to a good-quality small image, with the best quality that fits', async () => {
    const { deps, sizes } = browser([[100_000]]);

    const thumb = await createThumbRenderer(deps).render(new Blob(['x']));

    expect(thumb?.size).toBe(100_000);
    expect(sizes).toEqual([{ width: THUMB_LONG_SIDE, height: 480 }]);
  });

  it('tries a lower quality, and then a smaller size, when it weighs too much', async () => {
    const heavy = THUMB_MAX_BYTES + 1;
    const { deps, sizes } = browser([[heavy, heavy, heavy, heavy], [90_000]]);

    const thumb = await createThumbRenderer(deps).render(new Blob(['x']));

    expect(thumb?.size).toBe(90_000);
    expect(sizes).toHaveLength(2);
    expect(sizes[1]?.width).toBeLessThan(THUMB_LONG_SIDE);
  });

  it('gives nothing, instead of failing, when it never fits, the photo cannot be read or there is no canvas; and it always lets go of the image', async () => {
    const heavy = browser([[THUMB_MAX_BYTES + 1]]);
    expect(await createThumbRenderer(heavy.deps).render(new Blob(['x']))).toBeNull();
    expect(heavy.closed.count).toBe(1);

    expect(await createThumbRenderer({ decode: () => Promise.reject(new Error('bad')), createCanvas: heavy.deps.createCanvas }).render(new Blob(['x']))).toBeNull();

    const noCanvas = browser([[1]]);
    const withoutContext = { ...noCanvas.deps, createCanvas: () => ({ getContext: () => null, toJpeg: () => Promise.resolve(null) }) };
    expect(await createThumbRenderer(withoutContext).render(new Blob(['x']))).toBeNull();
  });
});
