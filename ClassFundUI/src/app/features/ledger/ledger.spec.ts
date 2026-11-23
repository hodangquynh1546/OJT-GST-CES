import { actionLabel, auditChanges } from '../audit/audit-format';
import { LedgerEntry, entryTags, formatMoneyInput, parseMoney, signedAmount, spendable } from './ledger.models';
import { EventSummary, dateBox, eventStats, isActiveEvent, longDate } from '../events/events.models';

function entry(partial: Partial<LedgerEntry>): LedgerEntry {
  return {
    id: 'e1',
    eventId: null,
    eventName: null,
    type: 'Expense',
    source: 'Manual',
    categoryId: null,
    categoryName: null,
    title: 'Mua hoa',
    amount: 650000,
    transactionDate: '2026-11-16',
    performedByUserId: 'u',
    performedByName: 'Huy',
    isSelfDeclared: false,
    reversesEntryId: null,
    reversedByEntryId: null,
    attachmentCount: 0,
    createdAt: '2026-11-16T11:42:00Z',
    ...partial,
  };
}

describe('ledger models (M7)', () => {
  it('parses and formats money input like the canvas', () => {
    expect(parseMoney('650.000')).toBe(650000);
    expect(parseMoney('650 000 đ')).toBe(650000);
    expect(parseMoney('')).toBeNull();
    expect(parseMoney('abc')).toBeNull();
    expect(formatMoneyInput(650000)).toBe('650.000');
    expect(formatMoneyInput(null)).toBe('');
  });

  it('signs amounts by type and never offers negative spending room', () => {
    expect(signedAmount(entry({ type: 'Income', amount: 200000 }))).toBe(200000);
    expect(signedAmount(entry({ type: 'Expense', amount: 200000 }))).toBe(-200000);
    expect(spendable(-5)).toBe(0);
    expect(spendable(4820000)).toBe(4820000);
  });

  it('builds row tags: receipts, reversal, self-declared payment', () => {
    expect(entryTags(entry({ attachmentCount: 2 })).map((tag) => tag.text)).toEqual(['2 chứng từ']);
    expect(entryTags(entry({ reversedByEntryId: 'r' }))).toEqual([{ text: 'Đã đảo', tone: 'danger' }]);
    expect(entryTags(entry({ type: 'Income', reversesEntryId: 'o' }))[0].text).toBe('Bút toán đảo');
    expect(entryTags(entry({ type: 'Income', source: 'Payment', isSelfDeclared: true }))).toEqual([{ text: 'Tự ghi nhận', tone: 'warning' }]);
    expect(entryTags(entry({ type: 'Income', source: 'Payment' }))[0].text).toBe('Từ thanh toán đã xác nhận');
  });
});

describe('event models (M8)', () => {
  const base: EventSummary = {
    id: 'e',
    name: 'Dã ngoại',
    description: null,
    eventDate: '2026-11-20',
    listDeadline: '2026-11-15',
    status: 'Open',
    invitedCount: 32,
    participantCount: 30,
    paidInFullCount: 20,
    totalIncome: 10700000,
    totalExpense: 7200000,
    balance: 3500000,
    myResponse: null,
    createdAt: '',
    completedAt: null,
  };

  it('formats card stats per status', () => {
    expect(eventStats(base)).toBe('30 tham gia · 20 đã đóng đủ · thu 10.700.000 đ · chi 7.200.000 đ');
    expect(eventStats({ ...base, status: 'Completed', participantCount: 28, totalIncome: 4150000, totalExpense: 4000000, balance: 150000 })).toBe(
      '28 tham gia · thu 4.150.000 đ · chi 4.000.000 đ · thừa 150.000 đ',
    );
    expect(eventStats({ ...base, status: 'Cancelled', totalIncome: 0, totalExpense: 0 })).toBe('Không phát sinh thu chi');
  });

  it('splits tabs and formats dates', () => {
    expect(isActiveEvent('Open')).toBeTrue();
    expect(isActiveEvent('Completed')).toBeTrue();
    expect(isActiveEvent('Settled')).toBeFalse();
    expect(dateBox('2026-11-20')).toEqual({ month: 'TH11', day: '20' });
    expect(dateBox(null)).toBeNull();
    expect(longDate('2026-11-20')).toBe('Thứ Sáu 20/11/2026');
  });
});

describe('audit labels for M7 / M8 (TD-14)', () => {
  it('translates new actions, fields and business dates', () => {
    expect(actionLabel('ledger.reverse')).toBe('Tạo bút toán đảo');
    expect(actionLabel('event.close_list')).toBe('Đóng danh sách sự kiện');
    const diff = auditChanges(null, '{"Name":"Dã ngoại","EventDate":"2026-11-20","invitedCount":32,"category":"Quà tặng"}');
    expect(diff.rows.map((row) => [row.label, row.after])).toEqual([
      ['Tên', 'Dã ngoại'],
      ['Ngày diễn ra', '20/11/2026'],
      ['Số người được mời', '32'],
      ['Danh mục', 'Quà tặng'],
    ]);
  });
});