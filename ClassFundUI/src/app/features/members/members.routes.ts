import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

export const MEMBER_ROUTES: Routes = [{ path: '', ...comingSoon('T-03', 'Thành viên') }];
