import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

export const TREASURER_ROUTES: Routes = [{ path: '', ...comingSoon('G-03', 'Phân công thủ quỹ') }];
