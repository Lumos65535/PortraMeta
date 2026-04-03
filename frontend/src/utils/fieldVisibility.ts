export const FIELD_VISIBILITY_KEY = 'portrameta_detail_fields';

export interface FieldDef {
  key: string;
  tier: 1 | 2 | 3;
  defaultVisible: boolean;
}

export const FIELD_DEFS: FieldDef[] = [
  { key: 'title', tier: 1, defaultVisible: true },
  { key: 'originalTitle', tier: 1, defaultVisible: true },
  { key: 'year', tier: 1, defaultVisible: true },
  { key: 'studioName', tier: 1, defaultVisible: true },
  { key: 'plot', tier: 1, defaultVisible: true },
  { key: 'directors', tier: 1, defaultVisible: true },
  { key: 'genres', tier: 1, defaultVisible: true },
  { key: 'tags', tier: 1, defaultVisible: true },
  { key: 'runtime', tier: 1, defaultVisible: true },
  { key: 'mpaa', tier: 1, defaultVisible: true },
  { key: 'premiered', tier: 1, defaultVisible: false },
  { key: 'userRating', tier: 1, defaultVisible: false },
  { key: 'ratings', tier: 1, defaultVisible: false },
  { key: 'uniqueIds', tier: 1, defaultVisible: false },
  { key: 'sortTitle', tier: 1, defaultVisible: false },
  { key: 'outline', tier: 2, defaultVisible: false },
  { key: 'tagline', tier: 2, defaultVisible: false },
  { key: 'credits', tier: 2, defaultVisible: false },
  { key: 'countries', tier: 2, defaultVisible: false },
  { key: 'setName', tier: 3, defaultVisible: false },
  { key: 'dateAdded', tier: 3, defaultVisible: false },
  { key: 'top250', tier: 3, defaultVisible: false },
];

export interface FieldVisibility {
  [key: string]: boolean;
}

export const DEFAULT_VISIBILITY: FieldVisibility = Object.fromEntries(
  FIELD_DEFS.map(f => [f.key, f.defaultVisible]),
);

export function getFieldVisibility(): FieldVisibility {
  try {
    const stored = localStorage.getItem(FIELD_VISIBILITY_KEY);
    if (stored) return { ...DEFAULT_VISIBILITY, ...JSON.parse(stored) };
  } catch { /* ignore */ }
  return { ...DEFAULT_VISIBILITY };
}

export function isFieldVisible(vis: FieldVisibility, key: string): boolean {
  return vis[key] ?? DEFAULT_VISIBILITY[key] ?? true;
}

export function getFieldLabelKey(key: string): string {
  if (key === 'studioName') return 'videoDetail.fields.studio';
  return `videoDetail.fields.${key}`;
}
