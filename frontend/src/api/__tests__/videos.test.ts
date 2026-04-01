import { describe, it, expect, vi, beforeEach } from 'vitest';
import { videosApi } from '../videos';
import { api } from '../client';

vi.mock('../client', () => {
  const mockApi = {
    get: vi.fn().mockResolvedValue({ data: { success: true, data: {} } }),
    put: vi.fn().mockResolvedValue({ data: { success: true, data: {} } }),
    post: vi.fn().mockResolvedValue({ data: { success: true, data: {} } }),
    defaults: { baseURL: '/api', headers: {} },
    interceptors: { response: { use: vi.fn(), handlers: [] } },
  };
  return { api: mockApi, ApiResponse: {} };
});

const mockApi = api as unknown as {
  get: ReturnType<typeof vi.fn>;
  put: ReturnType<typeof vi.fn>;
  post: ReturnType<typeof vi.fn>;
};

describe('videosApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('getAll passes filter params', async () => {
    await videosApi.getAll({ search: 'test', page: 2 });
    expect(mockApi.get).toHaveBeenCalledWith('/videos', {
      params: { search: 'test', page: 2 },
    });
  });

  it('getById calls correct URL', async () => {
    await videosApi.getById(123);
    expect(mockApi.get).toHaveBeenCalledWith('/videos/123');
  });

  it('update sends PUT with body', async () => {
    const body = { title: 'T', originalTitle: null, year: null, plot: null, studioName: null };
    await videosApi.update(5, body);
    expect(mockApi.put).toHaveBeenCalledWith('/videos/5', body);
  });

  it('getPosterUrl returns correct path', () => {
    expect(videosApi.getPosterUrl(42)).toBe('/api/videos/42/poster');
  });

  it('getFanartUrl returns correct path', () => {
    expect(videosApi.getFanartUrl(42)).toBe('/api/videos/42/fanart');
  });

  it('batchUpdate sends PUT to /videos/batch', async () => {
    const data = { ids: [1, 2], title: 'X' };
    await videosApi.batchUpdate(data);
    expect(mockApi.put).toHaveBeenCalledWith('/videos/batch', data);
  });

  it('batchDelete sends POST to /videos/batch/delete', async () => {
    const data = { ids: [1, 2], mode: 'Metadata' as const };
    await videosApi.batchDelete(data);
    expect(mockApi.post).toHaveBeenCalledWith('/videos/batch/delete', data);
  });

  it('uploadPoster sends FormData with undefined Content-Type', async () => {
    const file = new File(['data'], 'poster.jpg', { type: 'image/jpeg' });
    await videosApi.uploadPoster(1, file);
    expect(mockApi.post).toHaveBeenCalledWith(
      '/videos/1/poster',
      expect.any(FormData),
      { headers: { 'Content-Type': undefined } },
    );
  });

  it('importPosterFromPath sends path in body', async () => {
    await videosApi.importPosterFromPath(1, '/path/to/image.jpg');
    expect(mockApi.post).toHaveBeenCalledWith('/videos/1/poster/from-path', {
      path: '/path/to/image.jpg',
    });
  });
});
