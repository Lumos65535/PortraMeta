import { describe, it, expect, beforeEach } from 'vitest';
import zh from '../zh.json';
import en from '../en.json';

function getAllKeys(obj: Record<string, unknown>, prefix = ''): string[] {
  const keys: string[] = [];
  for (const [key, value] of Object.entries(obj)) {
    const fullKey = prefix ? `${prefix}.${key}` : key;
    if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
      keys.push(...getAllKeys(value as Record<string, unknown>, fullKey));
    } else {
      keys.push(fullKey);
    }
  }
  return keys.sort();
}

describe('i18n', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('zh and en have the same translation keys', () => {
    const zhKeys = getAllKeys(zh);
    const enKeys = getAllKeys(en);

    const missingInEn = zhKeys.filter(k => !enKeys.includes(k));
    const missingInZh = enKeys.filter(k => !zhKeys.includes(k));

    expect(missingInEn, `Keys in zh.json missing from en.json: ${missingInEn.join(', ')}`).toEqual([]);
    expect(missingInZh, `Keys in en.json missing from zh.json: ${missingInZh.join(', ')}`).toEqual([]);
  });

  it('default language is zh', async () => {
    const i18n = (await import('../index')).default;
    expect(i18n.language).toBe('zh');
  });

  it('setLanguage persists to localStorage', async () => {
    const { setLanguage } = await import('../index');
    setLanguage('en');
    expect(localStorage.getItem('portrameta_lang')).toBe('en');
  });

  it('setLanguage changes i18n language', async () => {
    const mod = await import('../index');
    mod.setLanguage('en');
    expect(mod.default.language).toBe('en');
    // Verify translation works
    expect(mod.default.t('nav.videos')).toBe('Videos');
  });

  it('all zh values are non-empty strings', () => {
    const keys = getAllKeys(zh);
    for (const key of keys) {
      const parts = key.split('.');
      let val: unknown = zh;
      for (const p of parts) val = (val as Record<string, unknown>)[p];
      expect(typeof val, `zh key "${key}" should be a string`).toBe('string');
      expect((val as string).length, `zh key "${key}" should not be empty`).toBeGreaterThan(0);
    }
  });

  it('all en values are non-empty strings', () => {
    const keys = getAllKeys(en);
    for (const key of keys) {
      const parts = key.split('.');
      let val: unknown = en;
      for (const p of parts) val = (val as Record<string, unknown>)[p];
      expect(typeof val, `en key "${key}" should be a string`).toBe('string');
      expect((val as string).length, `en key "${key}" should not be empty`).toBeGreaterThan(0);
    }
  });
});
