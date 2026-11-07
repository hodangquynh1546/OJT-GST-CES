import { environment } from '../../../environments/environment';

export const API_BASE_URL = environment.apiBaseUrl;

export function apiUrl(path: string): string {
  return `${API_BASE_URL}${path}`;
}

export function isApiRequest(url: string): boolean {
  return url.startsWith(API_BASE_URL);
}
