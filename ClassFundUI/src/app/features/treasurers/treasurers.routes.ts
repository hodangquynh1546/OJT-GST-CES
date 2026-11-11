import { Routes } from '@angular/router';

import './treasurers.errors';

/** Module Phân công thủ quỹ (M4) – G-03 tại `/funds/:fundId/treasurers` (guard Treasurer.Assign ở app.routes.ts). */
export const TREASURER_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./treasurers-page/treasurers-page').then((m) => m.TreasurersPage) },
];
