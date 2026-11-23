import { BalanceHistoryItem, historySource, withdrawError } from './balance.models';

describe('withdrawError (B-02)', () => {
  it('chấp nhận số tiền hợp lệ: ≥ 10.000, bội của 1.000, không vượt số dư', () => {
    expect(withdrawError(10_000, 50_000)).toBeNull();
    expect(withdrawError(50_000, 50_000)).toBeNull();
  });

  it('từ chối thiếu số tiền, dưới 10.000đ, không chẵn nghìn, vượt số dư', () => {
    expect(withdrawError(null, 50_000)).toContain('nhập số tiền');
    expect(withdrawError(9_000, 50_000)).toContain('tối thiểu 10.000');
    expect(withdrawError(10_500, 50_000)).toContain('bội của 1.000');
    expect(withdrawError(60_000, 50_000)).toContain('vượt');
    expect(withdrawError(-10_000, 50_000)).toContain('tối thiểu');
  });
});

describe('historySource', () => {
  const item = (patch: Partial<BalanceHistoryItem>): BalanceHistoryItem => ({
    id: '1',
    type: 'SettlementCredit',
    amount: 1000,
    balanceAfter: 1000,
    createdAt: '',
    description: '',
    scope: null,
    eventName: null,
    ...patch,
  });

  it('gọi tên nguồn của từng loại biến động', () => {
    expect(historySource(item({ type: 'Withdrawal' })).text).toBe('Rút tiền');
    expect(historySource(item({ type: 'ListCloseRefund', eventName: 'Dã ngoại' })).text).toBe('Hoàn khi đóng danh sách: Dã ngoại');
    expect(historySource(item({ scope: 'General' })).text).toBe('Quỹ chung');
  });
});
