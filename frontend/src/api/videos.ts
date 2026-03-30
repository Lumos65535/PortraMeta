import { api } from './client';
import type { ApiResponse } from './client';

export interface VideoFile {
  id: number;
  libraryId: number;
  fileName: string;
  filePath: string;
  fileSizeBytes: number;
  hasNfo: boolean;
  hasPoster: boolean;
  title: string | null;
  originalTitle: string | null;
  year: number | null;
  plot: string | null;
  studioName: string | null;
  scannedAt: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface VideoFilter {
  has_nfo?: boolean;
  has_poster?: boolean;
  library_id?: number;
  studio_id?: number;
  search?: string;
  page?: number;
  page_size?: number;
}

export const videosApi = {
  getAll: (filter: VideoFilter = {}) =>
    api.get<ApiResponse<PagedResult<VideoFile>>>('/videos', { params: filter }).then(r => r.data),
  getById: (id: number) =>
    api.get<ApiResponse<VideoFile>>(`/videos/${id}`).then(r => r.data),
};
