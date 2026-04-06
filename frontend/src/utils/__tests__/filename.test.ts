import { describe, expect, it } from 'vitest';
import { cleanForSearch } from '../filename';

describe('cleanForSearch', () => {
  it('removes extension and underscores', () => {
    expect(cleanForSearch('Studio_Title_1080p.mkv')).toBe('Studio Title');
  });

  it('removes dots and resolution token', () => {
    expect(cleanForSearch('My.Movie.720p.mp4')).toBe('My Movie');
  });

  it('removes brackets, hyphens, parentheses, and 4K token', () => {
    expect(cleanForSearch('[Studio] Some-Title (2024) 4K.avi')).toBe('Studio Some Title 2024');
  });

  it('handles simple filename', () => {
    expect(cleanForSearch('simple.mkv')).toBe('simple');
  });

  it('handles filename without extension', () => {
    expect(cleanForSearch('no_extension')).toBe('no extension');
  });

  it('removes multiple resolution tokens', () => {
    expect(cleanForSearch('Title_2160p_8K.mkv')).toBe('Title');
  });

  it('handles 720 without p suffix', () => {
    expect(cleanForSearch('Title_720.mp4')).toBe('Title');
  });

  it('handles 1080 without p suffix', () => {
    expect(cleanForSearch('Title_1080.mp4')).toBe('Title');
  });

  it('is case-insensitive for resolution tokens', () => {
    expect(cleanForSearch('Title_1080P.mkv')).toBe('Title');
    expect(cleanForSearch('Title_4k.mkv')).toBe('Title');
  });

  it('collapses multiple spaces', () => {
    expect(cleanForSearch('dots...and___underscores.mp4')).toBe('dots and underscores');
  });

  it('returns empty string for resolution-only filename', () => {
    expect(cleanForSearch('1080p.mkv')).toBe('');
  });

  it('removes en-dash and em-dash', () => {
    expect(cleanForSearch('Title\u2013Subtitle\u2014Extra.mkv')).toBe('Title Subtitle Extra');
  });

  it('splits camelCase words', () => {
    expect(cleanForSearch('myGreatMovie.mkv')).toBe('my Great Movie');
  });

  it('splits PascalCase words', () => {
    expect(cleanForSearch('MyGreatMovie.mkv')).toBe('My Great Movie');
  });

  it('splits uppercase sequences followed by camelCase', () => {
    expect(cleanForSearch('XMLParser.mkv')).toBe('XML Parser');
  });

  it('handles mixed symbols and camelCase', () => {
    expect(cleanForSearch('studio_myGreatTitle-1080p.mkv')).toBe('studio my Great Title');
  });

  it('works with NFO title input (no extension)', () => {
    expect(cleanForSearch('MyMovie')).toBe('My Movie');
  });

  it('handles title with dashes', () => {
    expect(cleanForSearch('Some\u2014Long Title')).toBe('Some Long Title');
  });
});
