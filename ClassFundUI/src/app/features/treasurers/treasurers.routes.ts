import { Routes } from '@angular/router';

import './treasurers.errors';
import { TreasurersPage } from './treasurers-page';

/** T-01 Thủ quỹ (cần `Treasurer.Manage` – chỉ thủ quỹ chính, guard ở `app.routes.ts`). */
export const TREASURER_ROUTES: Routes = [{ path: '', component: TreasurersPage, title: 'Thủ quỹ · ClassFund' }];
