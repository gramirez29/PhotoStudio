/**
 * Tells whether a file the person chose can be a ZIP: its name ends in `.zip` or the browser says it is one. Windows reports
 * `application/x-zip-compressed`, other systems `application/zip`, and some report nothing. The API checks what really arrives.
 * @param file The file.
 * @returns True when it can be a ZIP.
 */
export function isZipFile(file: Pick<File, 'name' | 'type'>): boolean {
  return /\.zip$/i.test(file.name) || file.type === 'application/zip' || file.type === 'application/x-zip-compressed';
}
