import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, act } from '@testing-library/react';
import { ThemeModeProvider, useThemeMode } from '../ThemeModeContext';

function TestConsumer() {
  const { themeMode, setThemeMode, resolvedMode } = useThemeMode();
  return (
    <div>
      <span data-testid="themeMode">{themeMode}</span>
      <span data-testid="resolvedMode">{resolvedMode}</span>
      <button onClick={() => setThemeMode('dark')}>Set Dark</button>
      <button onClick={() => setThemeMode('light')}>Set Light</button>
    </div>
  );
}

describe('ThemeModeContext', () => {
  beforeEach(() => {
    localStorage.clear();
    // Mock matchMedia to return light mode by default
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: false,
        media: query,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      })),
    });
  });

  it('defaults to system when no localStorage value', () => {
    const { getByTestId } = render(
      <ThemeModeProvider>
        <TestConsumer />
      </ThemeModeProvider>,
    );

    expect(getByTestId('themeMode').textContent).toBe('system');
  });

  it('reads initial value from localStorage', () => {
    localStorage.setItem('nfoforge_theme', 'dark');

    const { getByTestId } = render(
      <ThemeModeProvider>
        <TestConsumer />
      </ThemeModeProvider>,
    );

    expect(getByTestId('themeMode').textContent).toBe('dark');
    expect(getByTestId('resolvedMode').textContent).toBe('dark');
  });

  it('setThemeMode persists to localStorage', () => {
    const { getByText } = render(
      <ThemeModeProvider>
        <TestConsumer />
      </ThemeModeProvider>,
    );

    act(() => {
      getByText('Set Dark').click();
    });

    expect(localStorage.getItem('nfoforge_theme')).toBe('dark');
  });

  it('resolvedMode returns system preference when themeMode is system', () => {
    // Mock dark system preference
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: query === '(prefers-color-scheme: dark)',
        media: query,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      })),
    });

    const { getByTestId } = render(
      <ThemeModeProvider>
        <TestConsumer />
      </ThemeModeProvider>,
    );

    expect(getByTestId('themeMode').textContent).toBe('system');
    expect(getByTestId('resolvedMode').textContent).toBe('dark');
  });

  it('resolvedMode returns explicit light when set', () => {
    const { getByTestId, getByText } = render(
      <ThemeModeProvider>
        <TestConsumer />
      </ThemeModeProvider>,
    );

    act(() => {
      getByText('Set Light').click();
    });

    expect(getByTestId('resolvedMode').textContent).toBe('light');
  });

  it('useThemeMode throws outside provider', () => {
    expect(() => render(<TestConsumer />)).toThrow(
      'useThemeMode must be used within ThemeModeProvider',
    );
  });
});
