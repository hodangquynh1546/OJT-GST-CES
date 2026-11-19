import { Routes } from '@angular/router';

import './charges.errors';
import { ChargeDetailPage } from './charge-detail-page';
import { ChargesPage } from './charges-page';
import { MyPaymentsPage } from './my-payments-page';
import { PaymentsPage } from './payments-page';
import { Permissions } from '../../core/auth/permissions';
import { fundPermissionGuard } from '../../core/auth/auth.guards';

/** H-01 danh sách, H-02 tạo khoản thu (mở hộp thoại ngay), H-01b theo dõi một khoản. */
export const CHARGE_ROUTES: Routes = [
  { path: '', component: ChargesPage, title: 'Khoản thu chung · ClassFund' },
  { path: 'new', component: ChargesPage, data: { create: true }, canActivate: [fundPermissionGuard(Permissions.ChargeManage)], title: 'Tạo khoản thu · ClassFund' },
  { path: ':chargeId', component: ChargeDetailPage, canActivate: [fundPermissionGuard(Permissions.ChargeManage)], title: 'Theo dõi khoản thu · ClassFund' },
];

/** H-03 Khoản cần đóng của tôi (cần `Payment.Submit`, guard ở `app.routes.ts`). */
export const MY_PAYMENT_ROUTES: Routes = [{ path: '', component: MyPaymentsPage, title: 'Khoản cần đóng · ClassFund' }];

/** H-04 Xác nhận thanh toán (cần `Payment.Confirm`, guard ở `app.routes.ts`). */
export const PAYMENT_CONFIRM_ROUTES: Routes = [{ path: '', component: PaymentsPage, title: 'Xác nhận thanh toán · ClassFund' }];
