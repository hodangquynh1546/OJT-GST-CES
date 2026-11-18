import { Routes } from '@angular/router';

import '../auth/auth.errors';
import { AccountPage } from './account-page';

/** A-04 Tài khoản của tôi (nằm trong shell). */
export const ACCOUNT_ROUTES: Routes = [{ path: '', component: AccountPage, title: 'Tài khoản · ClassFund' }];
