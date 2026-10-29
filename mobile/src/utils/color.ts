/**
 * Gives a color some transparency.
 * @param hex Color as #rrggbb.
 * @param alpha How opaque the result is, from 0 (invisible) to 1 (the same color).
 * @returns The color as `rgba(r, g, b, a)`.
 */
export function withAlpha(hex: string, alpha: number): string {
  const [red = 0, green = 0, blue = 0] = [1, 3, 5].map((offset) => parseInt(hex.slice(offset, offset + 2), 16));
  const clamped = Math.min(Math.max(alpha, 0), 1);
  return `rgba(${red}, ${green}, ${blue}, ${clamped})`;
}
