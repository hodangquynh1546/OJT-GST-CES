import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { apiUrl, fundApiUrl } from '../../core/config/api.config';
import { AttachmentsApi } from './attachments.api';
import { formatFileSize, validateFile } from './attachments.models';
import { avatarInitials } from './avatar/avatar';

const MB = 1024 * 1024;

describe('attachments', () => {
  it('kiểm loại và dung lượng theo từng loại file (BR-07)', () => {
    expect(validateFile('Receipt', { type: 'application/pdf', size: 5 * MB })).toBeNull();
    expect(validateFile('Receipt', { type: 'application/pdf', size: 5 * MB + 1 })).toContain('tối đa 5 MB');
    expect(validateFile('Avatar', { type: 'application/pdf', size: 1000 })).toContain('Sai loại file');
    expect(validateFile('Avatar', { type: 'image/png', size: 2 * MB + 1 })).not.toBeNull();
    expect(validateFile('Document', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', size: 9 * MB })).toBeNull();
    expect(validateFile('PaymentProof', { type: 'image/gif', size: 100 })).not.toBeNull();
    expect(validateFile('PaymentProof', { type: 'image/png', size: 0 })).not.toBeNull();
  });

  it('định dạng dung lượng và chữ viết tắt', () => {
    expect(formatFileSize(1536 * 1024)).toBe('1,5 MB');
    expect(formatFileSize(340 * 1024)).toBe('340 KB');
    expect(avatarInitials('Nguyễn Minh Anh')).toBe('MA');
    expect(avatarInitials('An')).toBe('A');
  });

  describe('AttachmentsApi', () => {
    let api: AttachmentsApi;
    let backend: HttpTestingController;

    beforeEach(() => {
      TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
      api = TestBed.inject(AttachmentsApi);
      backend = TestBed.inject(HttpTestingController);
    });

    afterEach(() => backend.verify());

    it('upload multipart gồm file và kind', () => {
      const file = new File(['%PDF-1.7'], 'hd.pdf', { type: 'application/pdf' });
      api.upload('f1', 'Receipt', file).subscribe();
      const request = backend.expectOne(fundApiUrl('f1', '/attachments'));
      expect(request.request.method).toBe('POST');
      const body = request.request.body as FormData;
      expect(body.get('kind')).toBe('Receipt');
      expect((body.get('file') as File).name).toBe('hd.pdf');
    });

    it('tải file và ảnh đại diện dạng blob qua API', () => {
      api.download('f1', 'a1').subscribe();
      expect(backend.expectOne(fundApiUrl('f1', '/attachments/a1')).request.responseType).toBe('blob');

      api.avatar('u1').subscribe();
      expect(backend.expectOne(apiUrl('/users/u1/avatar')).request.responseType).toBe('blob');

      api.uploadAvatar(new File(['x'], 'a.png', { type: 'image/png' })).subscribe();
      expect(backend.expectOne(apiUrl('/auth/me/avatar')).request.method).toBe('PUT');
    });
  });
});
