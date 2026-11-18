import { Routes } from '@angular/router';

import { authGuard, fundPermissionGuard, guestGuard, homeGuard } from './core/auth/auth.guards';
import { Permission, Permissions } from './core/auth/permissions';
import { AppShell } from './core/navigation/app-shell';

/**
 * Bản đồ route (docs/ui-design/sitemap-and-navigation.md). File này thuộc Foundation:
 * mỗi module chỉ sửa `features/<module>/<module>.routes.ts` của mình (docs/implementation/module-plan.md).
 */
const fundChild = (path: string, permission: Permission, load: Routes[number]['loadChildren']) => ({
  path,
  canActivate: [fundPermissionGuard(permission)],
  loadChildren: load,
});

export const routes: Routes = [
  { path: '', pathMatch: 'full', canActivate: [homeGuard], children: [] },
  {
    path: '',
    canActivate: [guestGuard],
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.GUEST_ROUTES),
  },
  {
    path: 'change-password',
    canActivate: [authGuard],
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.CHANGE_PASSWORD_ROUTES),
  },
  // F-01 / F-02 cũng ngoài khung có menu: sau đăng nhập người dùng chọn quỹ rồi mới vào `funds/:fundId` (có menu).
  {
    path: 'funds',
    canActivate: [authGuard],
    loadChildren: () => import('./features/funds/funds.routes').then((m) => m.FUND_LIST_ROUTES),
  },
  // A-04 nằm ngoài khung có menu quỹ (canvas "ClassFund UI Design"): chỉ có thanh trên riêng.
  {
    path: 'account',
    canActivate: [authGuard],
    loadChildren: () => import('./features/account/account.routes').then((m) => m.ACCOUNT_ROUTES),
  },
  {
    path: '',
    component: AppShell,
    canActivate: [authGuard],
    children: [
      {
        path: 'funds/:fundId',
        canActivate: [fundPermissionGuard(Permissions.FundView)],
        children: [
          // Quỹ chung
          fundChild('ledger', Permissions.FundView, () => import('./features/ledger/ledger.routes').then((m) => m.LEDGER_ROUTES)),
          fundChild('charges', Permissions.FundView, () => import('./features/charges/charges.routes').then((m) => m.CHARGE_ROUTES)),
          // Sự kiện (gồm quyết toán: events/:eventId/settlement)
          fundChild('events', Permissions.EventParticipate, () => import('./features/events/events.routes').then((m) => m.EVENT_ROUTES)),
          // Việc của học sinh (thủ quỹ cũng có)
          fundChild('my-payments', Permissions.PaymentSubmit, () => import('./features/charges/charges.routes').then((m) => m.MY_PAYMENT_ROUTES)),
          fundChild('balance', Permissions.BalanceViewOwn, () => import('./features/balance/balance.routes').then((m) => m.BALANCE_ROUTES)),
          fundChild('notifications', Permissions.FundView, () => import('./features/notifications/notifications.routes').then((m) => m.NOTIFICATION_ROUTES)),
          // Quản lý của thủ quỹ
          fundChild('payments', Permissions.PaymentConfirm, () => import('./features/charges/charges.routes').then((m) => m.PAYMENT_CONFIRM_ROUTES)),
          fundChild('withdrawals', Permissions.BalanceHandle, () => import('./features/balance/balance.routes').then((m) => m.WITHDRAWAL_ROUTES)),
          fundChild('reports', Permissions.ReportView, () => import('./features/reports/reports.routes').then((m) => m.REPORT_ROUTES)),
          fundChild('members', Permissions.MemberManage, () => import('./features/members/members.routes').then((m) => m.MEMBER_ROUTES)),
          fundChild('treasurers', Permissions.TreasurerManage, () => import('./features/treasurers/treasurers.routes').then((m) => m.TREASURER_ROUTES)),
          fundChild('categories', Permissions.CategoryManage, () => import('./features/categories/categories.routes').then((m) => m.CATEGORY_ROUTES)),
          fundChild('audit', Permissions.AuditView, () => import('./features/audit/audit.routes').then((m) => m.AUDIT_ROUTES)),
          fundChild('settings', Permissions.FundManage, () => import('./features/funds/funds.routes').then((m) => m.FUND_SETTINGS_ROUTES)),
          {
            path: '',
            pathMatch: 'full',
            loadChildren: () => import('./features/overview/overview.routes').then((m) => m.OVERVIEW_ROUTES),
          },
        ],
      },
      {
        path: 'forbidden',
        loadComponent: () => import('./features/errors/forbidden-page').then((m) => m.ForbiddenPage),
      },
      {
        path: '**',
        loadComponent: () => import('./features/errors/not-found-page').then((m) => m.NotFoundPage),
      },
    ],
  },
];
