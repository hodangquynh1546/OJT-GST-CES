import { FundStatus } from '../funds/funds.models';
import { SettlementLine, SettlementOutcome, SurplusMethod } from '../events/events.models';

/** Khớp `YearEndPreviewDto` (S-05, xem trước tổng kết cuối năm). */
export interface YearEndPreview {
  fundName: string;
  schoolYear: string;
  status: FundStatus;
  memberCount: number;
  totalCollected: number;
  totalSpent: number;
  surplus: number;
  outcome: SettlementOutcome;
  unsettledEventCount: number;
  pendingPaymentCount: number;
  openChargeCount: number;
  unpaidObligationCount: number;
  canExecute: boolean;
  lines: SettlementLine[];
}

/** Khớp `YearEndResultDto`. */
export interface YearEndResult {
  id: string;
  fundName: string;
  schoolYear: string;
  outcome: SettlementOutcome;
  surplusMethod: SurplusMethod | null;
  memberCount: number;
  totalCollected: number;
  totalSpent: number;
  surplus: number;
  settledAt: string;
  settledByName: string;
  lines: SettlementLine[];
}
