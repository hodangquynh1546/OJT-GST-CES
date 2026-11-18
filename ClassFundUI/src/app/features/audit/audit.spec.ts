import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { fundApiUrl } from '../../core/config/api.config';
import { AuditApi } from './audit.api';
import { actionLabel, auditChanges, defaultRange, formatAuditTime, toApiDate } from './audit-format';

describe('audit format', () => {
  it('thời điểm UTC hiện theo giờ Việt Nam dạng dd/MM HH:mm', () => {
    expect(formatAuditTime('2026-11-17T02:40:00Z')).toBe('17/11 09:40');
    expect(formatAuditTime('2026-11-16T17:05:00Z')).toBe('17/11 00:05');
    expect(formatAuditTime('khong-hop-le')).toBe('');
  });

  it('nhãn hành động tiếng Việt, mã lạ giữ nguyên', () => {
    expect(actionLabel('fund.update')).toBe('Sửa thông tin quỹ');
    expect(actionLabel('treasurer.grant')).toBe('Phân quyền thủ quỹ');
    expect(actionLabel('payment.something_new')).toBe('payment.something_new');
  });

  it('trước / sau thành bảng tiếng Việt, đánh dấu trường đổi', () => {
    const category = auditChanges('{"Name":"Đóng góp quỹ lớp","IsActive":false}', '{"Name":"Đóng góp quỹ lớp","IsActive":true}');
    expect(category.hasBefore).toBeTrue();
    expect(category.rows).toEqual([
      { label: 'Tên', before: 'Đóng góp quỹ lớp', after: 'Đóng góp quỹ lớp', changed: false },
      { label: 'Trạng thái', before: 'Ngừng dùng', after: 'Đang dùng', changed: true },
    ]);

    const role = auditChanges('{"role":"Student"}', '{"role":"Treasurer"}');
    expect(role.rows[0]).toEqual({ label: 'Vai trò', before: 'Học sinh', after: 'Thủ quỹ', changed: true });
  });

  it('tạo mới chỉ có "sau"; ẩn mã định danh; định dạng tiền, dung lượng, loại file', () => {
    const upload = auditChanges(null, '{"kind":"Receipt","FileName":"hoa-don.pdf","ContentType":"application/pdf","SizeBytes":1572864}');
    expect(upload.hasBefore).toBeFalse();
    expect(upload.rows.map((row) => [row.label, row.after])).toEqual([
      ['Loại file', 'Hóa đơn'],
      ['Tên file', 'hoa-don.pdf'],
      ['Định dạng', 'PDF'],
      ['Dung lượng', '1,5 MB'],
    ]);

    const member = auditChanges(null, '{"userId":"u1","Email":"a@b.vn","role":"Student","outcome":"Created","amount":650000}');
    expect(member.rows.map((row) => row.label)).toEqual(['Email', 'Vai trò', 'Kết quả', 'Số tiền']);
    expect(member.rows[3].after).toBe('650.000 đ');

    const avatar = auditChanges('{"avatarAttachmentId":null}', '{"avatarAttachmentId":"a1"}');
    expect(avatar.rows[0]).toEqual({ label: 'Ảnh đại diện', before: 'Chưa có ảnh', after: 'Ảnh mới', changed: true });
  });

  it('rỗng → không có dòng; không phải JSON → một dòng "Nội dung"', () => {
    expect(auditChanges(null, '  ')).toEqual({ hasBefore: false, hasAfter: false, rows: [] });
    expect(auditChanges(null, 'abc').rows).toEqual([{ label: 'Nội dung', before: '–', after: 'abc', changed: false }]);
  });

  it('ngày cho API và khoảng mặc định từ đầu tháng tới hôm nay', () => {
    expect(toApiDate(new Date(2026, 10, 7))).toBe('2026-11-07');
    expect(toApiDate(null)).toBeNull();
    const range = defaultRange(new Date(2026, 10, 17, 15, 30));
    expect(toApiDate(range.from)).toBe('2026-11-01');
    expect(toApiDate(range.to)).toBe('2026-11-17');
  });
});

describe('AuditApi', () => {
  let api: AuditApi;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(AuditApi);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('chỉ gửi bộ lọc có giá trị; "Tất cả" không gửi scope; selfOnly chỉ khi bật', () => {
    api.list('f1', { scope: 'all', page: 1, pageSize: 20 }).subscribe();
    const plain = backend.expectOne((request) => request.url === fundApiUrl('f1', '/audit-logs'));
    expect(plain.request.params.keys().sort()).toEqual(['page', 'pageSize']);

    api
      .list('f1', { from: '2026-11-01', to: '2026-11-17', actorId: 'u1', scope: 'Event', selfOnly: true, page: 2, pageSize: 20 })
      .subscribe();
    const filtered = backend.expectOne((request) => request.url === fundApiUrl('f1', '/audit-logs'));
    expect(filtered.request.params.get('from')).toBe('2026-11-01');
    expect(filtered.request.params.get('to')).toBe('2026-11-17');
    expect(filtered.request.params.get('actorId')).toBe('u1');
    expect(filtered.request.params.get('scope')).toBe('Event');
    expect(filtered.request.params.get('selfOnly')).toBe('true');
    expect(filtered.request.params.get('page')).toBe('2');
  });

  it('danh sách người thực hiện', () => {
    api.actors('f1').subscribe();
    expect(backend.expectOne(fundApiUrl('f1', '/audit-logs/actors')).request.method).toBe('GET');
  });
});
