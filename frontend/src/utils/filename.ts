/**
 * Filename processing utilities.
 * Foundation for future filename tokenization and smart parsing features.
 */

/** Resolution/quality tokens to strip from filenames */
const RESOLUTION_PATTERN = /\b(720p?|1080p?|2160p?|4320p?|4k|8k)\b/gi;

/** Symbol characters to replace with spaces (includes en-dash, em-dash, and common symbols) */
const SYMBOL_PATTERN = /[_\-.\[\](){}\u2013\u2014]/g;

/** CamelCase / PascalCase boundary: insert space before uppercase letter preceded by lowercase */
const CAMEL_CASE_PATTERN = /([a-z])([A-Z])/g;

/** Sequence of uppercase letters followed by an uppercase+lowercase pair (e.g., "XMLParser" → "XML Parser") */
const UPPER_SEQUENCE_PATTERN = /([A-Z]+)([A-Z][a-z])/g;

/**
 * Clean a filename into a human-readable search query.
 * Removes extension, symbols, resolution tokens, and splits camelCase words.
 */
export function cleanForSearch(filename: string): string {
  let name = filename;
  const dotIndex = name.lastIndexOf('.');
  if (dotIndex > 0) name = name.substring(0, dotIndex);
  name = name.replace(SYMBOL_PATTERN, ' ');
  name = name.replace(CAMEL_CASE_PATTERN, '$1 $2');
  name = name.replace(UPPER_SEQUENCE_PATTERN, '$1 $2');
  name = name.replace(RESOLUTION_PATTERN, '');
  name = name.replace(/\s+/g, ' ').trim();
  return name;
}
