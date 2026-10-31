import { describe, expect, it, vi } from 'vitest';
import { PROOF_MAX_BYTES } from '../constants/proof';
import type { ProofCanvas, ProofRendererDeps } from '../types/media/proofRenderer.types';
import { createProofRenderer, ProofRenderError } from './proofRenderer';

/** What a fake canvas records, so the tests can say what was drawn. */
interface Drawn {
  /** Width of the canvases created, in order. */
  readonly widths: number[];
  /** Calls to `drawImage`: the image and its rectangle. */
  readonly draws: { readonly image: unknown; readonly args: readonly number[] }[];
  /** Qualities asked to the encoder, in order. */
  readonly qualities: number[];
  /** Values of `globalAlpha` seen at each draw. */
  readonly alphas: number[];
  /** Closed images. */
  closed: number;
}

/**
 * Makes the dependencies of the renderer with fakes: bitmaps of a given size, and a canvas that records its drawings and encodes
 * to a size chosen per quality.
 * @param original Size of the photo.
 * @param sizeOf Weight in bytes of the JPEG for a quality and a canvas width.
 * @returns The dependencies and what they recorded.
 */
function fakeDeps(original: { width: number; height: number }, sizeOf: (quality: number, width: number) => number): { deps: ProofRendererDeps; drawn: Drawn } {
  const drawn: Drawn = { widths: [], draws: [], qualities: [], alphas: [], closed: 0 };
  const bitmap = (width: number, height: number): ImageBitmap => ({ width, height, close: () => void drawn.closed++ }) as unknown as ImageBitmap;
  const mark = bitmap(640, 640);
  const photo = bitmap(original.width, original.height);
  const deps: ProofRendererDeps = {
    decode: () => Promise.resolve(photo),
    loadWatermark: () => Promise.resolve(mark),
    createCanvas: (size): ProofCanvas => {
      drawn.widths.push(size.width);
      const context = {
        globalAlpha: 1,
        shadowBlur: 0,
        shadowColor: '',
        save: () => undefined,
        restore: () => undefined,
        drawImage(image: unknown, ...args: number[]) {
          drawn.draws.push({ image, args });
          drawn.alphas.push(context.globalAlpha);
        },
      };
      return {
        getContext: () => context as unknown as CanvasRenderingContext2D,
        toJpeg: (quality) => {
          drawn.qualities.push(quality);
          return Promise.resolve(new Blob([new Uint8Array(sizeOf(quality, size.width))], { type: 'image/jpeg' }));
        },
      };
    },
  };
  return { deps, drawn };
}

const photoFile = new File([new Uint8Array(3)], 'IMG_1.jpg', { type: 'image/jpeg' });

describe('the renderer of proofs', () => {
  it('reduces the photo to the size of a proof and draws the watermark in the middle, a little see-through', async () => {
    const { deps, drawn } = fakeDeps({ width: 6000, height: 4000 }, () => 1000);
    const proof = await createProofRenderer(deps).render(photoFile);

    expect(proof.size).toEqual({ width: 720, height: 480 });
    expect(proof.quality).toBe(0.4);
    expect(proof.blob.size).toBe(1000);
    expect(drawn.draws).toHaveLength(2);
    expect(drawn.draws[0]?.args).toEqual([0, 0, 720, 480]);
    const [x, y, width, height] = drawn.draws[1]?.args ?? [];
    expect(width).toBeCloseTo(336);
    expect(x).toBeCloseTo((720 - 336) / 2);
    expect(y).toBeCloseTo((480 - (height ?? 0)) / 2);
    expect(drawn.alphas).toEqual([1, 0.6]);
  });

  it('closes the photo it decoded, also when it fails', async () => {
    const { deps, drawn } = fakeDeps({ width: 100, height: 100 }, () => 1000);
    await createProofRenderer(deps).render(photoFile);
    expect(drawn.closed).toBe(1);

    const failing = fakeDeps({ width: 100, height: 100 }, () => PROOF_MAX_BYTES + 1);
    await expect(createProofRenderer(failing.deps).render(photoFile)).rejects.toMatchObject({ kind: 'too_big' });
    expect(failing.drawn.closed).toBe(1);
  });

  it('lowers the quality until the proof fits in the limit', async () => {
    const { deps, drawn } = fakeDeps({ width: 3000, height: 2000 }, (quality) => (quality > 0.25 ? PROOF_MAX_BYTES + 1 : 500_000));
    const proof = await createProofRenderer(deps).render(photoFile);
    expect(drawn.qualities).toEqual([0.4, 0.3, 0.2]);
    expect(proof.quality).toBe(0.2);
  });

  it('makes the proof smaller when even the lowest quality is too heavy', async () => {
    const { deps, drawn } = fakeDeps({ width: 3000, height: 2000 }, (_quality, width) => (width > 600 ? PROOF_MAX_BYTES + 1 : 400_000));
    const proof = await createProofRenderer(deps).render(photoFile);
    expect(drawn.widths).toEqual([720, 540]);
    expect(proof.size.width).toBe(540);
  });

  it('gives up when the proof is too heavy at every size', async () => {
    const { deps, drawn } = fakeDeps({ width: 3000, height: 2000 }, () => PROOF_MAX_BYTES + 1);
    await expect(createProofRenderer(deps).render(photoFile)).rejects.toBeInstanceOf(ProofRenderError);
    expect(drawn.widths).toHaveLength(4);
  });

  it('says the photo could not be read when it cannot be decoded', async () => {
    const { deps } = fakeDeps({ width: 10, height: 10 }, () => 1);
    await expect(createProofRenderer({ ...deps, decode: () => Promise.reject(new Error('corrupt')) }).render(photoFile)).rejects.toMatchObject({ kind: 'decode' });
  });

  it('says the browser cannot draw when there is no drawing context or no encoder', async () => {
    const { deps } = fakeDeps({ width: 10, height: 10 }, () => 1);
    const noContext = { ...deps, createCanvas: (): ProofCanvas => ({ getContext: () => null, toJpeg: () => Promise.resolve(null) }) };
    await expect(createProofRenderer(noContext).render(photoFile)).rejects.toMatchObject({ kind: 'canvas' });

    const noEncoder = { ...deps, createCanvas: (): ProofCanvas => ({ getContext: () => ({ drawImage: vi.fn(), save: vi.fn(), restore: vi.fn() }) as unknown as CanvasRenderingContext2D, toJpeg: () => Promise.resolve(null) }) };
    await expect(createProofRenderer(noEncoder).render(photoFile)).rejects.toMatchObject({ kind: 'canvas' });
  });

  it('loads the watermark once, however many photos it renders', async () => {
    const { deps } = fakeDeps({ width: 100, height: 100 }, () => 10);
    const loadWatermark = vi.fn(deps.loadWatermark);
    const renderer = createProofRenderer({ ...deps, loadWatermark });
    await renderer.render(photoFile);
    await renderer.render(photoFile);
    expect(loadWatermark).toHaveBeenCalledTimes(1);
  });
});
