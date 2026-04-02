/**
 * Filename processing utilities.
 * Foundation for future filename tokenization and smart parsing features.
 */

/** Resolution/quality tokens to strip from filenames */
const RESOLUTION_PATTERN = /\b(720p?|1080p?|2160p?|4320p?|4k|8k)\b/gi;

/** Symbol characters to replace with spaces */
const SYMBOL_PATTERN = /[_\-.\[\](){}]/g;

/**
 * Clean a filename into a human-readable search query.
 * Removes extension, symbols, and resolution tokens.
 */
export function cleanForSearch(filename: string): string {
  let name = filename;
  const dotIndex = name.lastIndexOf('.');
  if (dotIndex > 0) name = name.substring(0, dotIndex);
  name = name.replace(SYMBOL_PATTERN, ' ');
  name = name.replace(RESOLUTION_PATTERN, '');
  name = name.replace(/\s+/g, ' ').trim();
  return name;
}
