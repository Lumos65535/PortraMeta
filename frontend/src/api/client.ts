import axios from 'axios';

const BASE_URL = import.meta.env.DEV ? '' : (import.meta.env.VITE_API_URL ?? 'http://localhost:5001');

export const api = axios.create({
  baseURL: BASE_URL ? `${BASE_URL}/api` : '/api',
  headers: { 'Content-Type': 'application/json' },
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
    const message = serverError ?? (err.message === 'Network Error' ? '无法连接到服务器' : '请求失败');
    return Promise.reject(new Error(message));
  },
);
