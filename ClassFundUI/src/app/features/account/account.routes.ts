import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

export const ACCOUNT_ROUTES: Routes = [{ path: '', ...comingSoon('A-04', 'Tài khoản') }];
