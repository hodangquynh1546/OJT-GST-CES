import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';

export const STUDENT_ROUTES: Routes = [{ path: '', ...comingSoon('G-02', 'Học sinh & phụ huynh') }];
