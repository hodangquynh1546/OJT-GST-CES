import { Routes } from '@angular/router';

import { fundPermissionGuard } from '../../core/auth/auth.guards';
import { Permissions } from '../../core/auth/permissions';
import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

export const FUND_LIST_ROUTES: Routes = [{ path: '', ...comingSoon('G-01', 'Quỹ của tôi') }];

export const FUND_ROUTES: Routes = [
  { path: '', pathMatch: 'full', ...comingSoon('S-01', 'Tổng quan') },
  {
    path: 'settings',
    canActivate: [fundPermissionGuard(Permissions.FundManage)],
    ...comingSoon('G-04', 'Thông tin quỹ'),
  },
];
