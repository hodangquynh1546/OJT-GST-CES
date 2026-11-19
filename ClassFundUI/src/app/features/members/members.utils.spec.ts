import { countInvalidStudents, credentialsText, parseStudentLines, uniqueStudents } from './members.utils';

describe('members.utils (OQ-20)', () => {
  it('mỗi dòng là email rồi họ tên; chịu được tab, dấu phẩy, chấm phẩy và dòng trống', () => {
    expect(
      parseStudentLines('hodangquynh2k4@gmail.com Hồ Đăng Quỳnh\n\nb@x.com,Lê Thu Hà\r\nc@x.com;\td@x.com\t Phạm  Đức Long \n   \n'),
    ).toEqual([
      { email: 'hodangquynh2k4@gmail.com', fullName: 'Hồ Đăng Quỳnh' },
      { email: 'b@x.com', fullName: 'Lê Thu Hà' },
      { email: 'c@x.com', fullName: 'd@x.com\t Phạm  Đức Long' },
    ]);
    expect(parseStudentLines('   \n  ')).toEqual([]);
  });

  it('dòng chỉ có email thì họ tên trống', () => {
    expect(parseStudentLines('a@x.com')).toEqual([{ email: 'a@x.com', fullName: '' }]);
  });

  it('bỏ dòng trùng email không phân biệt hoa thường, giữ dòng đầu tiên', () => {
    expect(
      uniqueStudents([
        { email: 'A@x.com', fullName: 'Một' },
        { email: 'b@x.com', fullName: '' },
        { email: 'a@X.COM', fullName: 'Hai' },
      ]),
    ).toEqual([
      { email: 'A@x.com', fullName: 'Một' },
      { email: 'b@x.com', fullName: '' },
    ]);
  });

  it('đếm email không hợp lệ theo cùng quy tắc với đăng ký', () => {
    const lines = ['a@x.com', 'khong-hop-le', 'a@@x.com', 'b@x'].map((email) => ({ email, fullName: '' }));
    expect(countInvalidStudents(lines)).toBe(3);
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
