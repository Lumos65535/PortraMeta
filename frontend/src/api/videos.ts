import { api } from './client';
import type { ApiResponse } from './client';

export interface Actor {
  id: number;
  name: string;
  role: string | null;
  order: number;
}

export interface VideoFile {
  id: number;
  libraryId: number;
  fileName: string;
  filePath: string;
  fileSizeBytes: number;
  hasNfo: boolean;
  hasPoster: boolean;
  hasFanart: boolean;
  title: string | null;
  originalTitle: string | null;
  year: number | null;
  plot: string | null;
  studioName: string | null;
  scannedAt: string;
  actors: Actor[] | null;
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

export interface ActorRequest {
  name: string;
  role: string | null;
  order: number;
}

export interface UpdateVideoRequest {
  title: string | null;
  originalTitle: string | null;
  year: number | null;
  plot: string | null;
  studioName: string | null;
  actors?: ActorRequest[];
}

export const videosApi = {
  getAll: (filter: VideoFilter = {}) =>
    api.get<ApiResponse<PagedResult<VideoFile>>>('/videos', { params: filter }).then(r => r.data),
  getById: (id: number) =>
    api.get<ApiResponse<VideoFile>>(`/videos/${id}`).then(r => r.data),
  update: (id: number, data: UpdateVideoRequest) =>
    api.put<ApiResponse<VideoFile>>(`/videos/${id}`, data).then(r => r.data),
  getPosterUrl: (id: number): string => `/api/videos/${id}/poster`,
  uploadPoster: (id: number, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return api
      .post<ApiResponse<VideoFile>>(`/videos/${id}/poster`, formData, {
        headers: { 'Content-Type': undefined },
      })
      .then(r => r.data);
  },
  importPosterFromPath: (id: number, path: string) =>
    api.post<ApiResponse<VideoFile>>(`/videos/${id}/poster/from-path`, { path }).then(r => r.data),
  getFanartUrl: (id: number): string => `/api/videos/${id}/fanart`,
  uploadFanart: (id: number, file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return api
      .post<ApiResponse<VideoFile>>(`/videos/${id}/fanart`, formData, {
        headers: { 'Content-Type': undefined },
      })
      .then(r => r.data);
  },
  importFanartFromPath: (id: number, path: string) =>
    api.post<ApiResponse<VideoFile>>(`/videos/${id}/fanart/from-path`, { path }).then(r => r.data),
};
