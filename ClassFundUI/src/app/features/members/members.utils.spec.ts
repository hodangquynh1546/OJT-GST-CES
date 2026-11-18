import { countInvalidEmails, credentialsText, parseEmails, uniqueEmails } from './members.utils';

describe('members.utils (OQ-20)', () => {
  it('tách email theo xuống dòng, dấu phẩy, chấm phẩy và khoảng trắng; bỏ phần trống', () => {
    expect(parseEmails('a@x.com\nb@x.com, c@x.com;  d@x.com\t\n\n e@x.com ')).toEqual([
      'a@x.com',
      'b@x.com',
      'c@x.com',
      'd@x.com',
      'e@x.com',
    ]);
    expect(parseEmails('   \n  ')).toEqual([]);
  });

  it('bỏ email trùng không phân biệt hoa thường, giữ thứ tự đầu tiên', () => {
    expect(uniqueEmails(['A@x.com', 'b@x.com', 'a@X.COM', ' B@x.com '])).toEqual(['A@x.com', 'b@x.com']);
  });

  it('đếm email không hợp lệ theo cùng quy tắc với đăng ký', () => {
    expect(countInvalidEmails(['a@x.com', 'khong-hop-le', 'a@@x.com', 'b@x'])).toBe(3);
  });

  it('nội dung sao chép: mỗi dòng email<TAB>mật khẩu', () => {
    expect(
      credentialsText([
        { email: 'a@x.com', password: 'Tam-1' },
        { email: 'b@x.com', password: 'Tam-2' },
      ]),
    ).toBe('a@x.com\tTam-1\nb@x.com\tTam-2');
  });
});
