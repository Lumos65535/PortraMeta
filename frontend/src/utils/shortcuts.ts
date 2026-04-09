// Keyboard shortcut system — data-driven, extensible for future Settings page customization

export type ShortcutAction =
  | 'nav.prev' | 'nav.next' | 'nav.back'
  | 'edit.start' | 'edit.save' | 'edit.cancel'
  | 'actor.add' | 'actor.removeLast';

export interface ShortcutBinding {
  key: string;        // KeyboardEvent.key value (case-sensitive)
  ctrl?: boolean;     // Ctrl (Windows/Linux) or Cmd (macOS)
  shift?: boolean;
  alt?: boolean;
}

export type ShortcutMap = Record<ShortcutAction, ShortcutBinding[]>;

export const DEFAULT_SHORTCUTS: ShortcutMap = {
  'nav.prev':         [{ key: 'ArrowLeft' }, { key: '[' }],
  'nav.next':         [{ key: 'ArrowRight' }, { key: ']' }],
  'nav.back':         [{ key: 'Escape' }],
  'edit.start':       [{ key: 'e' }],
  'edit.save':        [{ key: 's', ctrl: true }],
  'edit.cancel':      [{ key: 'Escape' }],
  'actor.add':        [{ key: 'A', shift: true }],
  'actor.removeLast': [{ key: 'D', shift: true }],
};

const STORAGE_KEY = 'portrameta_shortcuts';

export function getShortcuts(): ShortcutMap {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as Partial<ShortcutMap>;
      return { ...DEFAULT_SHORTCUTS, ...parsed };
    }
  } catch { /* ignore corrupt data */ }
  return { ...DEFAULT_SHORTCUTS };
}

export function saveShortcuts(map: ShortcutMap): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(map));
}

export function resetShortcuts(): void {
  localStorage.removeItem(STORAGE_KEY);
}

const KEY_DISPLAY: Record<string, string> = {
  ArrowLeft: '←',
  ArrowRight: '→',
  ArrowUp: '↑',
  ArrowDown: '↓',
  Escape: 'Esc',
  ' ': 'Space',
  Enter: 'Enter',
};

export function formatBinding(b: ShortcutBinding): string {
  const parts: string[] = [];
  if (b.ctrl) parts.push('Ctrl');
  if (b.shift) parts.push('Shift');
  if (b.alt) parts.push('Alt');
  parts.push(KEY_DISPLAY[b.key] ?? b.key.toUpperCase());
  return parts.join('+');
}

export function formatAction(action: ShortcutAction): string {
  const map = getShortcuts();
  const bindings = map[action];
  if (!bindings?.length) return '';
  return bindings.map(formatBinding).join(' / ');
}

export function matchBinding(e: KeyboardEvent, b: ShortcutBinding): boolean {
  const ctrlMatch = b.ctrl ? (e.ctrlKey || e.metaKey) : !(e.ctrlKey || e.metaKey);
  const shiftMatch = b.shift ? e.shiftKey : !e.shiftKey;
  const altMatch = b.alt ? e.altKey : !e.altKey;
  return e.key === b.key && ctrlMatch && shiftMatch && altMatch;
}
