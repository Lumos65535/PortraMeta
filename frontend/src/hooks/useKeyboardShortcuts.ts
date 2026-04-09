import { useEffect, useRef } from 'react';
import { getShortcuts, matchBinding } from '../utils/shortcuts';
import type { ShortcutAction } from '../utils/shortcuts';

export type ShortcutHandlers = Partial<Record<ShortcutAction, (() => void) | null>>;

export interface ShortcutOptions {
  disabled?: boolean;
}

function isInputElement(el: EventTarget | null): boolean {
  if (!el || !(el instanceof HTMLElement)) return false;
  const tag = el.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || el.isContentEditable;
}

export function useKeyboardShortcuts(
  handlers: ShortcutHandlers,
  options?: ShortcutOptions,
): void {
  const handlersRef = useRef(handlers);
  const optionsRef = useRef(options);

  useEffect(() => { handlersRef.current = handlers; }, [handlers]);
  useEffect(() => { optionsRef.current = options; }, [options]);

  useEffect(() => {
    const shortcutMap = getShortcuts();

    const onKeyDown = (e: KeyboardEvent) => {
      if (optionsRef.current?.disabled) return;

      const inInput = isInputElement(e.target);

      for (const [action, bindings] of Object.entries(shortcutMap)) {
        const matched = bindings.some(b => matchBinding(e, b));
        if (!matched) continue;

        // In input fields, only allow edit.save (Ctrl/Cmd+S) through
        if (inInput && action !== 'edit.save') continue;

        const handler = handlersRef.current[action as ShortcutAction];
        if (!handler) continue;

        e.preventDefault();
        handler();
        return;
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);
}
