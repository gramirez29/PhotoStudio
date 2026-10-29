import { withAlpha } from '../color';

describe('withAlpha', () => {
  it('writes the color with its transparency', () => {
    expect(withAlpha('#0b1a40', 0.4)).toBe('rgba(11, 26, 64, 0.4)');
    expect(withAlpha('#ffffff', 1)).toBe('rgba(255, 255, 255, 1)');
  });

  it('keeps the transparency between 0 and 1', () => {
    expect(withAlpha('#000000', -2)).toBe('rgba(0, 0, 0, 0)');
    expect(withAlpha('#000000', 7)).toBe('rgba(0, 0, 0, 1)');
  });
});
