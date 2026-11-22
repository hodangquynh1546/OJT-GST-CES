import { Routes } from '@angular/router';

import { fundPermissionGuard } from '../../core/auth/auth.guards';
import { Permissions } from '../../core/auth/permissions';
import './events.errors';

/** M8 – Sự kiện (guard Event.Participate ở app.routes.ts). E-02 là hộp thoại của E-01 nhưng vẫn mở thẳng được bằng `…/events/new`. */
export const EVENT_ROUTES: Routes = [
  {
    path: '',
    title: 'Sự kiện · ClassFund',
    loadComponent: () => import('./event-list/event-list').then((m) => m.EventList),
  },
  {
    path: 'new',
    title: 'Tạo sự kiện · ClassFund',
    canActivate: [fundPermissionGuard(Permissions.EventManage)],
    data: { openNew: true },
    loadComponent: () => import('./event-list/event-list').then((m) => m.EventList),
  },
  {
    path: ':eventId',
    title: 'Chi tiết sự kiện · ClassFund',
    loadComponent: () => import('./event-detail/event-detail').then((m) => m.EventDetail),
  },
  {
    path: ':eventId/settlement',
    title: 'Quyết toán sự kiện · ClassFund',
    loadComponent: () => import('./settlement-page/settlement-page').then((m) => m.SettlementPage),
  },
];
