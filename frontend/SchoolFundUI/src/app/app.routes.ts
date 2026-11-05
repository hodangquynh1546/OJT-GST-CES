import { Routes } from '@angular/router';

import { authGuard, fundPermissionGuard, guestGuard, homeGuard } from './core/auth/auth.guards';
import { Permissions } from './core/auth/permissions';
import { AppShell } from './core/navigation/app-shell';
import { comingSoon } from './shared/components/coming-soon/coming-soon';

/** Route theo docs/ui-design/sitemap-and-navigation.md mục 1. */
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
    ...comingSoon('A-03', 'Đặt mật khẩu mới'),
  },
  {
    path: '',
    component: AppShell,
    canActivate: [authGuard],
    children: [
      {
        path: 'account',
        loadChildren: () => import('./features/account/account.routes').then((m) => m.ACCOUNT_ROUTES),
      },
      {
        path: 'funds',
        loadChildren: () => import('./features/funds/funds.routes').then((m) => m.FUND_LIST_ROUTES),
      },
      {
        path: 'funds/:fundId',
        canActivate: [fundPermissionGuard(Permissions.FundView)],
        children: [
          {
            path: 'transactions',
            loadChildren: () =>
              import('./features/transactions/transactions.routes').then((m) => m.TRANSACTION_ROUTES),
          },
          {
            path: 'reports',
            loadChildren: () => import('./features/reports/reports.routes').then((m) => m.REPORT_ROUTES),
          },
          {
            path: 'students',
            canActivate: [fundPermissionGuard(Permissions.StudentManage)],
            loadChildren: () =>
              import('./features/students/students.routes').then((m) => m.STUDENT_ROUTES),
          },
          {
            path: 'treasurers',
            canActivate: [fundPermissionGuard(Permissions.TreasurerAssign)],
            loadChildren: () =>
              import('./features/treasurers/treasurers.routes').then((m) => m.TREASURER_ROUTES),
          },
          {
            path: 'categories',
            canActivate: [fundPermissionGuard(Permissions.CategoryManage)],
            loadChildren: () =>
              import('./features/categories/categories.routes').then((m) => m.CATEGORY_ROUTES),
          },
          {
            path: 'members',
            canActivate: [fundPermissionGuard(Permissions.MemberView)],
            loadChildren: () => import('./features/members/members.routes').then((m) => m.MEMBER_ROUTES),
          },
          {
            path: '',
            loadChildren: () => import('./features/funds/funds.routes').then((m) => m.FUND_ROUTES),
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
