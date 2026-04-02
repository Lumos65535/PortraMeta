import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import zh from './zh.json';
import en from './en.json';

const STORAGE_KEY = 'portrameta_lang';

i18n
  .use(initReactI18next)
  .init({
    resources: {
      zh: { translation: zh },
      en: { translation: en },
    },
    lng: localStorage.getItem(STORAGE_KEY) ?? 'zh',
    fallbackLng: 'zh',
    interpolation: { escapeValue: false },
  });

export function setLanguage(lang: 'zh' | 'en') {
  localStorage.setItem(STORAGE_KEY, lang);
  i18n.changeLanguage(lang);
}

export default i18n;
