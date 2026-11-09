import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

/** Module Tổng quan & Báo cáo (M7) – S-01 tại `/funds/:fundId`. */
export const OVERVIEW_ROUTES: Routes = [{ path: '', ...comingSoon('S-01', 'Tổng quan') }];
