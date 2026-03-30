import { api } from './client';
import type { ApiResponse } from './client';

export interface Library {
  id: number;
  name: string;
  path: string;
  createdAt: string;
}

export const librariesApi = {
  getAll: () => api.get<ApiResponse<Library[]>>('/libraries').then(r => r.data),
  create: (name: string, path: string) =>
    api.post<ApiResponse<Library>>('/libraries', { name, path }).then(r => r.data),
  delete: (id: number) => api.delete(`/libraries/${id}`),
  scan: (id: number) => api.post<ApiResponse<string>>(`/libraries/${id}/scan`).then(r => r.data),
};
