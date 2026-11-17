/** Khớp ClassFund.Domain.Authorization.Permissions ở backend (nguồn sự thật: RolePermissions.cs). */
export const Permissions = {
  FundCreate: 'Fund.Create',
  FundView: 'Fund.View',
  FundManage: 'Fund.Manage',
  MemberManage: 'Member.Manage',
  TreasurerManage: 'Treasurer.Manage',
  CategoryManage: 'Category.Manage',
  EventManage: 'Event.Manage',
  EventParticipate: 'Event.Participate',
  ChargeManage: 'Charge.Manage',
  LedgerManage: 'Ledger.Manage',
  PaymentSubmit: 'Payment.Submit',
  PaymentConfirm: 'Payment.Confirm',
  SettlementExecute: 'Settlement.Execute',
  BalanceViewOwn: 'Balance.ViewOwn',
  BalanceWithdraw: 'Balance.Withdraw',
  BalanceHandle: 'Balance.Handle',
  ReportView: 'Report.View',
  AuditView: 'Audit.View',
} as const;

export type Permission = (typeof Permissions)[keyof typeof Permissions];
