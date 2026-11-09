import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** Module Quỹ (M2) – G-01 tại `/funds`. */
export const FUND_LIST_ROUTES: Routes = [{ path: '', ...comingSoon('G-01', 'Quỹ của tôi') }];

/** Module Quỹ (M2) – G-04 tại `/funds/:fundId/settings` (guard Fund.Manage ở app.routes.ts). */
export const FUND_SETTINGS_ROUTES: Routes = [{ path: '', ...comingSoon('G-04', 'Thông tin quỹ') }];
