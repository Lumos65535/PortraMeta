import { api } from './client';
import type { ApiResponse } from './client';

export interface Actor {
  id: number;
  name: string;
  role: string | null;
  order: number;
}

export interface Rating {
  name: string;
  value: number;
  votes: number;
  max: number;
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
  fileModifiedAt: string | null;
  // Tier 1
  directors: string[] | null;
  genres: string[] | null;
  runtime: number | null;
  mpaa: string | null;
  premiered: string | null;
  ratings: Rating[] | null;
  userRating: number | null;
  uniqueIds: Record<string, string> | null;
  tags: string[] | null;
  sortTitle: string | null;
  // Tier 2
  outline: string | null;
  tagline: string | null;
  credits: string[] | null;
  countries: string[] | null;
  // Tier 3
  setName: string | null;
  dateAdded: string | null;
  top250: number | null;
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
  has_fanart?: boolean;
  library_id?: number;
  studio_id?: number;
  search?: string;
  page?: number;
  page_size?: number;
  sort_by?: string;
  sort_desc?: boolean;
  filters?: string;
  filter_logic?: string;
}

export interface AdvancedFilterItem {
  field: string;
  op: string;
  value: string;
}

export interface ActorRequest {
  name: string;
  role: string | null;
  order: number;
}

export interface RatingRequest {
  name: string;
  value: number;
  votes: number;
  max: number;
}

export interface UpdateVideoRequest {
  title: string | null;
  originalTitle: string | null;
  year: number | null;
  plot: string | null;
  studioName: string | null;
  actors?: ActorRequest[];
  // Tier 1
  directors?: string[] | null;
  genres?: string[] | null;
  runtime?: number | null;
  mpaa?: string | null;
  premiered?: string | null;
  ratings?: RatingRequest[] | null;
  userRating?: number | null;
  uniqueIds?: Record<string, string> | null;
  tags?: string[] | null;
  sortTitle?: string | null;
  // Tier 2
  outline?: string | null;
  tagline?: string | null;
  credits?: string[] | null;
  countries?: string[] | null;
  // Tier 3
  setName?: string | null;
  dateAdded?: string | null;
  top250?: number | null;
}

export interface BatchUpdateRequest {
  ids: number[];
  title?: string | null;
  originalTitle?: string | null;
  year?: number | null;
  plot?: string | null;
  studioName?: string | null;
  directors?: string[] | null;
  genres?: string[] | null;
  runtime?: number | null;
  mpaa?: string | null;
  premiered?: string | null;
  userRating?: number | null;
  tags?: string[] | null;
  sortTitle?: string | null;
  outline?: string | null;
  tagline?: string | null;
  credits?: string[] | null;
  countries?: string[] | null;
  setName?: string | null;
  dateAdded?: string | null;
  top250?: number | null;
}

export interface BatchUpdateResult {
  updated: number;
  failed: number[];
}

export type DeleteMode = 'Metadata' | 'Video' | 'All';

export interface BatchDeleteRequest {
  ids: number[];
  mode: DeleteMode;
}

export interface BatchDeleteResult {
  deleted: number;
  failed: number[];
}

export interface FilterOptions {
  studios: string[];
  setNames: string[];
}

export const videosApi = {
  getFilterOptions: () =>
    api.get<ApiResponse<FilterOptions>>('/videos/filter-options').then(r => r.data),
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
  batchUpdate: (data: BatchUpdateRequest) =>
    api.put<ApiResponse<BatchUpdateResult>>('/videos/batch', data).then(r => r.data),
  batchDelete: (data: BatchDeleteRequest) =>
    api.post<ApiResponse<BatchDeleteResult>>('/videos/batch/delete', data).then(r => r.data),
  revealInFileManager: (id: number) =>
    api.post<ApiResponse<void>>(`/videos/${id}/reveal`).then(r => r.data),
  openVideoFile: (id: number) =>
    api.post<ApiResponse<void>>(`/videos/${id}/open`).then(r => r.data),
};
