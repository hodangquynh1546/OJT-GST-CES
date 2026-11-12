import { Routes } from '@angular/router';

/** Module Thành viên (M5) – T-03 tại `/funds/:fundId/members` (guard Member.View ở app.routes.ts). */
export const MEMBER_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./members-page/members-page').then((m) => m.MembersPage) },
];
