import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

export const REPORT_ROUTES: Routes = [{ path: '', ...comingSoon('S-03', 'Báo cáo') }];
