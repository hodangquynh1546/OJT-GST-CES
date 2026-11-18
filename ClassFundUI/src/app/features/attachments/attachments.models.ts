/** Khớp `AttachmentKind` ở backend. */
export type AttachmentKind = 'Receipt' | 'PaymentProof' | 'Avatar' | 'Document';

/** Khớp `AttachmentDto` – không bao giờ có URL bucket; tải file qua API. */
export interface Attachment {
  id: string;
  kind: AttachmentKind;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
}

export interface FileLimit {
  contentTypes: readonly string[];
  /** Thuộc tính `accept` của ô chọn file. */
  accept: string;
  maxBytes: number;
  description: string;
}

const MB = 1024 * 1024;
const IMAGES = ['image/jpeg', 'image/png', 'image/webp'];
const PDF = ['application/pdf'];
const OFFICE = [
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
];

/** Giới hạn theo loại – khớp `FileRule.Limits` (BR-07). Backend vẫn kiểm lại (cả chữ ký đầu file). */
export const FILE_LIMITS: Record<AttachmentKind, FileLimit> = {
  Receipt: {
    contentTypes: [...IMAGES, ...PDF],
    accept: '.jpg,.jpeg,.png,.webp,.pdf',
    maxBytes: 5 * MB,
    description: 'Ảnh JPG/PNG/WEBP hoặc PDF, tối đa 5 MB',
  },
  PaymentProof: {
    contentTypes: [...IMAGES, ...PDF],
    accept: '.jpg,.jpeg,.png,.webp,.pdf',
    maxBytes: 5 * MB,
    description: 'Ảnh JPG/PNG/WEBP hoặc PDF, tối đa 5 MB',
  },
  Avatar: {
    contentTypes: IMAGES,
    accept: '.jpg,.jpeg,.png,.webp',
    maxBytes: 2 * MB,
    description: 'Ảnh JPG/PNG/WEBP, tối đa 2 MB',
  },
  Document: {
    contentTypes: [...IMAGES, ...PDF, ...OFFICE],
    accept: '.jpg,.jpeg,.png,.webp,.pdf,.docx,.xlsx',
    maxBytes: 10 * MB,
    description: 'PDF, Word, Excel hoặc ảnh, tối đa 10 MB',
  },
};

/** Kiểm tra trước khi gửi để báo lỗi ngay; trả thông báo lỗi hoặc null. */
export function validateFile(kind: AttachmentKind, file: Pick<File, 'type' | 'size'>): string | null {
  const limit = FILE_LIMITS[kind];
  if (!limit.contentTypes.includes(file.type.toLowerCase())) {
    return `Sai loại file. ${limit.description}.`;
  }
  if (file.size <= 0 || file.size > limit.maxBytes) {
    return `File quá lớn hoặc rỗng. ${limit.description}.`;
  }
  return null;
}

export function isImage(contentType: string): boolean {
  return contentType.startsWith('image/');
}

/** `1.2 MB`, `340 KB`. */
export function formatFileSize(bytes: number): string {
  if (bytes >= MB) {
    return `${(bytes / MB).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} MB`;
  }
  return `${Math.max(1, Math.round(bytes / 1024)).toLocaleString('vi-VN')} KB`;
}
