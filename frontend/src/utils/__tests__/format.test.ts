import { describe, expect, it } from 'vitest';
import { formatBytes, splitComma } from '../format';

describe('formatBytes', () => {
  it('formats bytes below 1 KB', () => {
    expect(formatBytes(0)).toBe('0 B');
    expect(formatBytes(1023)).toBe('1023 B');
  });

  it('formats kilobytes', () => {
    expect(formatBytes(1024)).toBe('1.0 KB');
    expect(formatBytes(1536)).toBe('1.5 KB');
  });

  it('formats megabytes', () => {
    expect(formatBytes(1024 * 1024)).toBe('1.0 MB');
    expect(formatBytes(2.5 * 1024 * 1024)).toBe('2.5 MB');
  });

  it('formats gigabytes with two decimals', () => {
    expect(formatBytes(1024 * 1024 * 1024)).toBe('1.00 GB');
    expect(formatBytes(5.25 * 1024 * 1024 * 1024)).toBe('5.25 GB');
  });
});

describe('splitComma', () => {
  it('splits comma-separated values', () => {
    expect(splitComma('a,b,c')).toEqual(['a', 'b', 'c']);
  });

  it('trims whitespace around items', () => {
    expect(splitComma(' a , b ,c ')).toEqual(['a', 'b', 'c']);
  });

  it('filters out empty items', () => {
    expect(splitComma('a,,b,')).toEqual(['a', 'b']);
  });

  it('returns null for empty string', () => {
    expect(splitComma('')).toBeNull();
  });

  it('returns null when only commas and spaces', () => {
    expect(splitComma(' , , ')).toBeNull();
  });
});
