/** Khớp SchoolFund.Domain.Authorization.Permissions ở backend. */
export const Permissions = {
  FundCreate: 'Fund.Create',
  FundView: 'Fund.View',
  LedgerViewFull: 'Ledger.ViewFull',
  LedgerViewPublic: 'Ledger.ViewPublic',
  FundManage: 'Fund.Manage',
  StudentManage: 'Student.Manage',
  TreasurerAssign: 'Treasurer.Assign',
  MemberView: 'Member.View',
  TransactionManage: 'Transaction.Manage',
  CategoryManage: 'Category.Manage',
} as const;

export type Permission = (typeof Permissions)[keyof typeof Permissions];
