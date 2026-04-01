import { describe, it, expect, vi, beforeEach } from 'vitest';
import axios from 'axios';

// We need to test the module's behavior, so we re-import fresh each time
describe('API client', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  it('creates axios instance with /api baseURL', async () => {
    const mod = await import('../client');
    // In test env, DEV is true so BASE_URL is '' → baseURL should be '/api'
    expect(mod.api.defaults.baseURL).toBe('/api');
  });

  it('has Content-Type application/json', async () => {
    const mod = await import('../client');
    expect(mod.api.defaults.headers['Content-Type']).toBe('application/json');
  });

  it('error interceptor extracts server error message', async () => {
    const mod = await import('../client');
    // Simulate a response error through the interceptor
    const interceptor = mod.api.interceptors.response as unknown as {
      handlers: Array<{ rejected: (err: unknown) => unknown }>;
    };

    // Get the rejection handler (last one added)
    const handler = interceptor.handlers[interceptor.handlers.length - 1];

    const mockError = {
      response: { data: { error: 'Custom server error' } },
      message: 'Request failed',
    };

    await expect(handler.rejected(mockError)).rejects.toThrow('Custom server error');
  });

  it('error interceptor uses fallback for network error', async () => {
    const mod = await import('../client');
    const interceptor = mod.api.interceptors.response as unknown as {
      handlers: Array<{ rejected: (err: unknown) => unknown }>;
    };
    const handler = interceptor.handlers[interceptor.handlers.length - 1];

    const mockError = {
      response: undefined,
      message: 'Network Error',
    };

    await expect(handler.rejected(mockError)).rejects.toThrow('无法连接到服务器');
  });

  it('error interceptor uses generic fallback for other errors', async () => {
    const mod = await import('../client');
    const interceptor = mod.api.interceptors.response as unknown as {
      handlers: Array<{ rejected: (err: unknown) => unknown }>;
    };
    const handler = interceptor.handlers[interceptor.handlers.length - 1];

    const mockError = {
      response: { data: {} },
      message: 'timeout of 5000ms exceeded',
    };

    await expect(handler.rejected(mockError)).rejects.toThrow('请求失败');
  });
});
