import { Routes } from '@angular/router';

import './members.errors';
import { MembersPage } from './members-page';

/** M-01 Học sinh (cần `Member.Manage`, guard ở `app.routes.ts`). */
export const MEMBER_ROUTES: Routes = [{ path: '', component: MembersPage, title: 'Học sinh · ClassFund' }];
