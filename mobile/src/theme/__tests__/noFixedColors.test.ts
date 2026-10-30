import { readdirSync, readFileSync } from 'node:fs';
import { join, relative } from 'node:path';

/** The folders of the app that draw the interface. */
const UI_FOLDERS = ['app', 'components'];

/**
 * Lists the source files of a folder, without the tests.
 * @param folder Folder to read.
 * @returns The paths of its .ts and .tsx files, tests excluded.
 */
function sourceFiles(folder: string): readonly string[] {
  return readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
    const path = join(folder, entry.name);
    if (entry.isDirectory()) {
      return entry.name === '__tests__' ? [] : sourceFiles(path);
    }
    return /\.tsx?$/.test(entry.name) ? [path] : [];
  });
}

const sourceRoot = join(__dirname, '..', '..');
const files = UI_FOLDERS.flatMap((folder) => sourceFiles(join(sourceRoot, folder)));

describe('the interface takes its colors from the theme', () => {
  it('reads at least the screens and the components', () => {
    expect(files.length).toBeGreaterThan(50);
  });

  it('writes no color: no hex, no rgb(), in any screen or component', () => {
    const offenders = files.filter((file) => /#[0-9a-fA-F]{6}\b|#[0-9a-fA-F]{3}\b|\brgba?\(/.test(readFileSync(file, 'utf8')));

    expect(offenders.map((file) => relative(sourceRoot, file))).toEqual([]);
  });

  it('keeps nothing from the fixed colors of the old tokens file', () => {
    const offenders = files.filter((file) => /import \{[^}]*\bcolors\b[^}]*\} from '[./]*theme\/tokens'/.test(readFileSync(file, 'utf8')));

    expect(offenders.map((file) => relative(sourceRoot, file))).toEqual([]);
  });

  it('writes no font weight: the weight is a font family, because the fonts of the app are one file per weight', () => {
    const offenders = files.filter((file) => /fontWeight: '(?:[5-9]00|bold)'/.test(readFileSync(file, 'utf8')));

    // The check mark and the chevron of the select are symbols, not text of the app: they keep the weight of the system.
    expect(offenders.map((file) => relative(sourceRoot, file))).toEqual([join('components', 'SelectField.tsx')]);
  });
});
