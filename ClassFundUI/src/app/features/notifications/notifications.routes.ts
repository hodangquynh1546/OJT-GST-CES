import { Routes } from '@angular/router';

/** M11 – Thông báo (guard Fund.View ở app.routes.ts). */
export const NOTIFICATION_ROUTES: Routes = [
  {
    path: '',
    title: 'Thông báo · ClassFund',
    loadComponent: () => import('./notifications-page/notifications-page').then((m) => m.NotificationsPage),
  },
];
