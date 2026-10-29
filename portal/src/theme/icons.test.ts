import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

/** Folder of the files the site serves as they are. */
const publicDir = resolve(__dirname, '../../public');

/** The page that links the icons, read as text. */
const html = readFileSync(resolve(__dirname, '../../index.html'), 'utf8');

/** The manifest that names the icon for the home screen. */
const manifest = JSON.parse(readFileSync(resolve(publicDir, 'site.webmanifest'), 'utf8')) as {
  readonly name: string;
  readonly theme_color: string;
  readonly icons: readonly { readonly src: string; readonly sizes: string }[];
};

/**
 * Reads the pixel size of a PNG from its header.
 * @param file Name of a file in the public folder.
 * @returns The width and the height.
 */
function pngSize(file: string): readonly [number, number] {
  const bytes = readFileSync(resolve(publicDir, file));
  return [bytes.readUInt32BE(16), bytes.readUInt32BE(20)];
}

describe('the icon of the site', () => {
  it.each(['/favicon.ico', '/favicon-32.png', '/icon-192.png', '/apple-touch-icon.png', '/site.webmanifest'])(
    'links %s from the page, and the file exists',
    (href) => {
      expect(html).toContain(`href="${href}"`);
      expect(existsSync(resolve(publicDir, href.slice(1)))).toBe(true);
    },
  );

  it('names the studio and the color of the band for the browser bar', () => {
    expect(manifest.name).toBe('PhotoSense Studio');
    expect(manifest.theme_color).toBe('#3a4466');
    expect(html).toContain('name="theme-color" content="#3A4466"');
  });

  it('lists icons that exist and have the size the manifest says', () => {
    expect(manifest.icons.length).toBeGreaterThan(0);
    for (const icon of manifest.icons) {
      const [width] = icon.sizes.split('x').map(Number);
      expect(pngSize(icon.src.slice(1))).toEqual([width, width]);
    }
  });

  it('packs the 32 and 48 pixel icons in the ICO the browsers ask for by default', () => {
    const ico = readFileSync(resolve(publicDir, 'favicon.ico'));

    expect(ico.readUInt16LE(2)).toBe(1);
    expect(ico.readUInt16LE(4)).toBe(2);
  });
});
