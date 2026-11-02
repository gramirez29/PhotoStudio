import { describe, expect, it, vi } from 'vitest';
import { downloadInSequence, toDownloads, totalBytes } from './downloads';

const FINALS = [
  { id: 'a', fileName: 'IMG_0001.jpg', url: 'https://s/a', expiresAt: 'x', bytes: 1000, isExtra: false },
  { id: 'b', fileName: 'IMG_0002.jpg', url: 'https://s/b', expiresAt: 'x', bytes: 2500, isExtra: true },
];

describe('the downloads of the client', () => {
  it('turns finals into addresses and names, and adds up their size', () => {
    expect(toDownloads(FINALS)).toEqual([
      { url: 'https://s/a', fileName: 'IMG_0001.jpg' },
      { url: 'https://s/b', fileName: 'IMG_0002.jpg' },
    ]);
    expect(totalBytes(FINALS)).toBe(3500);
  });

  it('starts the downloads one after another with a pause between them, and none after the last', async () => {
    const order: string[] = [];
    const wait = vi.fn((milliseconds: number) => {
      order.push(`wait ${milliseconds}`);
      return Promise.resolve();
    });

    await downloadInSequence(toDownloads(FINALS), (item) => order.push(item.fileName), 700, (started) => order.push(`started ${started}`), wait);

    expect(order).toEqual(['IMG_0001.jpg', 'started 1', 'wait 700', 'IMG_0002.jpg', 'started 2']);
  });
});
