import axios from 'axios';

// 开发模式下使用相对 URL，由 Vite proxy 转发；生产模式下使用环境变量
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
