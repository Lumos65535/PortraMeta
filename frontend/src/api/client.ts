import axios from 'axios';
import i18n from '../i18n';

const BASE_URL = import.meta.env.DEV ? '' : (import.meta.env.VITE_API_URL ?? '');

// Optional API key — set VITE_API_KEY at build time or leave empty to disable.
const API_KEY = import.meta.env.VITE_API_KEY ?? '';

export const api = axios.create({
  baseURL: BASE_URL ? `${BASE_URL}/api` : '/api',
  headers: {
    'Content-Type': 'application/json',
    ...(API_KEY ? { 'X-Api-Key': API_KEY } : {}),
  },
});

export interface ApiResponse<T> {
  data: T;
  success: boolean;
  error?: string;
}

// Global error interceptor — re-throws with a user-friendly message
api.interceptors.response.use(
  res => res,
  err => {
    const serverError: string | undefined = err.response?.data?.error;
    const message = serverError ?? (err.message === 'Network Error' ? i18n.t('errors.networkError') : i18n.t('errors.requestFailed'));
    return Promise.reject(new Error(message));
  },
);
