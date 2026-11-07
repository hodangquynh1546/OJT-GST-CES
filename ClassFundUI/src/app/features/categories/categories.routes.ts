import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

export const CATEGORY_ROUTES: Routes = [{ path: '', ...comingSoon('T-02', 'Danh mục thu chi') }];
