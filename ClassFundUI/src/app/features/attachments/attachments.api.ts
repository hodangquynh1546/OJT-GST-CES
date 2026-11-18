import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiUrl, fundApiUrl } from '../../core/config/api.config';
import { Attachment, AttachmentKind } from './attachments.models';

/**
 * M5 – File trên Neon Object Storage, luôn đi qua API (kiểm quyền AZ-05).
 * Upload trước (nhận `id`), gửi `attachmentIds` kèm giao dịch / thanh toán ở M6, M7.
 */
@Injectable({ providedIn: 'root' })
export class AttachmentsApi {
  private readonly http = inject(HttpClient);

  upload(fundId: string, kind: Exclude<AttachmentKind, 'Avatar'>, file: File): Observable<Attachment> {
    const body = new FormData();
    body.append('file', file, file.name);
    body.append('kind', kind);
    return this.http.post<Attachment>(fundApiUrl(fundId, '/attachments'), body);
  }

  download(fundId: string, attachmentId: string): Observable<Blob> {
    return this.http.get(fundApiUrl(fundId, `/attachments/${encodeURIComponent(attachmentId)}`), { responseType: 'blob' });
  }

  /** Đặt / thay ảnh đại diện của tôi (FR-04). */
  uploadAvatar(file: File): Observable<{ attachmentId: string }> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.put<{ attachmentId: string }>(apiUrl('/auth/me/avatar'), body);
  }

  /** Ảnh đại diện của mình hoặc người cùng quỹ; 404 khi chưa có ảnh. */
  avatar(userId: string): Observable<Blob> {
    return this.http.get(apiUrl(`/users/${encodeURIComponent(userId)}/avatar`), { responseType: 'blob' });
  }
}

/** Lưu blob thành file tải về với tên gốc. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}
