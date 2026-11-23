import { AppNotification, notificationLink, splitTitle } from './notifications.models';

const base: AppNotification = {
  id: 'n1',
  scope: 'General',
  eventId: null,
  type: 'GeneralChargeCreated',
  title: '',
  body: '',
  referenceType: null,
  referenceId: null,
  isRead: false,
  createdAt: '2026-11-20T03:00:00Z',
};

describe('notifications.models', () => {
  it('splits the scope prefix off the title', () => {
    expect(splitTitle('[Quỹ chung] Khoản thu mới: Quỹ HK1')).toEqual({ text: 'Khoản thu mới: Quỹ HK1', eventName: null });
    expect(splitTitle('[Sự kiện: Dã ngoại Ba Vì] Tiền xe')).toEqual({ text: 'Tiền xe', eventName: 'Dã ngoại Ba Vì' });
    expect(splitTitle('Không có tiền tố')).toEqual({ text: 'Không có tiền tố', eventName: null });
  });

  it('links each notification type to the place it is about', () => {
    expect(notificationLink('f1', { ...base, type: 'EventChargeCreated', eventId: 'e1' })).toEqual(['/funds', 'f1', 'events', 'e1']);
    expect(notificationLink('f1', { ...base, type: 'SettlementResult', eventId: 'e1' })).toEqual(['/funds', 'f1', 'events', 'e1', 'settlement']);
    expect(notificationLink('f1', { ...base, type: 'BalanceCredited' })).toEqual(['/funds', 'f1', 'balance']);
    expect(notificationLink('f1', { ...base, type: 'WithdrawalProcessed' })).toEqual(['/funds', 'f1', 'balance']);
    expect(notificationLink('f1', { ...base, type: 'ChargeDueSoon' })).toEqual(['/funds', 'f1', 'my-payments']);
    expect(notificationLink('f1', { ...base, type: 'EventCreated' })).toEqual(['/funds', 'f1', 'events']);
  });
});
