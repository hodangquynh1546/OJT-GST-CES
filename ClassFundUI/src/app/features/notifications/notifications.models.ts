import { FinancialScope } from '../../shared/models/labels';

export type NotificationType =
  | 'EventCreated'
  | 'GeneralChargeCreated'
  | 'EventChargeCreated'
  | 'ChargeDueSoon'
  | 'EventParticipantExcluded'
  | 'SettlementResult'
  | 'BalanceCredited'
  | 'WithdrawalProcessed'
  | 'PaymentConfirmed'
  | 'PaymentRejected'
  | 'EventUnpaidReminder';

/** Khớp `NotificationDto`. Tiêu đề đã có tiền tố phạm vi "[Quỹ chung]" / "[Sự kiện: tên]". */
export interface AppNotification {
  id: string;
  scope: FinancialScope;
  eventId: string | null;
  type: NotificationType;
  title: string;
  body: string;
  referenceType: string | null;
  referenceId: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationList {
  items: AppNotification[];
  total: number;
  page: number;
  pageSize: number;
  unreadCount: number;
}

export const NOTIFICATION_PAGE_SIZE = 20;

const PREFIX = /^\[([^\]]*)\]\s*/;

/** Tách tiền tố phạm vi khỏi tiêu đề: badge hiện phạm vi, tiêu đề chỉ còn nội dung. */
export function splitTitle(title: string): { text: string; eventName: string | null } {
  const match = PREFIX.exec(title);
  if (!match) {
    return { text: title, eventName: null };
  }
  const eventName = /^Sự kiện:\s*(.+)$/.exec(match[1])?.[1] ?? null;
  return { text: title.slice(match[0].length), eventName };
}

/** Đường dẫn mở khi bấm vào thông báo (theo loại và đối tượng liên quan). */
export function notificationLink(fundId: string, item: AppNotification): string[] {
  const base = ['/funds', fundId];
  switch (item.type) {
    case 'EventCreated':
    case 'EventChargeCreated':
    case 'EventParticipantExcluded':
    case 'EventUnpaidReminder':
      return item.eventId ? [...base, 'events', item.eventId] : [...base, 'events'];
    case 'SettlementResult':
      // Tổng kết cuối năm (không có sự kiện): phần tiền thừa của tôi xem ở lịch sử số dư.
      return item.eventId ? [...base, 'events', item.eventId, 'settlement'] : [...base, 'balance'];
    case 'BalanceCredited':
    case 'WithdrawalProcessed':
      return [...base, 'balance'];
    default:
      return [...base, 'my-payments'];
  }
}
