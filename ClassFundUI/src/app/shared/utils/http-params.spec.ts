import { toHttpParams } from './http-params';

describe('toHttpParams', () => {
  it('bỏ giá trị rỗng, giữ số và boolean', () => {
    const params = toHttpParams({ page: 1, pageSize: 20, search: '', type: null, status: undefined, includeHidden: false });
    expect(params.toString()).toBe('page=1&pageSize=20&includeHidden=false');
  });
});
