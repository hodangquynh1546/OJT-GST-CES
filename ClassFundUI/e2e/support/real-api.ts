import { APIRequestContext, expect } from '@playwright/test';

/**
 * Chế độ **full** (`E2E_FULL=1`): gọi API thật (qua proxy Angular → ClassFund.Api → PostgreSQL) để seed dữ liệu
 * cho test, giống cách SPA gọi: cookie phiên + header X-XSRF-TOKEN; token mới lấy từ response có `antiforgeryToken`.
 * Seed bằng chính API của các module đã xong (ví dụ đăng ký GVCN → tạo quỹ), không ghi thẳng DB.
 */
export class RealApi {
  private token: string | null = null;

  constructor(private readonly request: APIRequestContext) {}

  async get<T>(path: string): Promise<T> {
    const response = await this.request.get(`/api/v1${path}`);
    expect(response.ok(), `${path}: ${response.status()} ${await response.text()}`).toBeTruthy();
    return (await response.json()) as T;
  }

  async post<T>(path: string, body?: unknown): Promise<T> {
    return this.send<T>('POST', path, body);
  }

  async put<T>(path: string, body: unknown): Promise<T> {
    return this.send<T>('PUT', path, body);
  }

  private async send<T>(method: 'POST' | 'PUT', path: string, body: unknown): Promise<T> {
    const response = await this.request.fetch(`/api/v1${path}`, {
      method,
      data: body,
      headers: { 'X-XSRF-TOKEN': await this.antiforgery() },
    });
    expect(response.ok(), `${method} ${path}: ${response.status()} ${await response.text()}`).toBeTruthy();
    const text = await response.text();
    const json = text ? (JSON.parse(text) as T & { antiforgeryToken?: string }) : (undefined as T);
    if (json && typeof json === 'object' && 'antiforgeryToken' in json && json.antiforgeryToken) {
      this.token = json.antiforgeryToken;
    }
    return json;
  }

  private async antiforgery(): Promise<string> {
    if (!this.token) {
      this.token = (await this.get<{ antiforgeryToken: string }>('/auth/antiforgery')).antiforgeryToken;
    }
    return this.token;
  }
}

/** Email / tên không trùng giữa các lần chạy (DB e2e dùng chung). */
export function unique(prefix: string): string {
  return `${prefix}${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

export const isFullE2E = process.env['E2E_FULL'] === '1';
