import { Routes } from '@angular/router';

import { comingSoon } from '../../shared/components/coming-soon/coming-soon';
import { FundListPage } from './fund-list-page';

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const FUND_LIST_ROUTES: Routes = [
  // F-01: màn đầu tiên sau đăng nhập, ngoài khung menu quỹ (M1 làm phần điều hướng; M2 bổ sung số liệu thẻ và Tạo quỹ).
  { path: '', component: FundListPage, title: 'Quỹ của tôi · ClassFund' },
  { path: 'new', ...comingSoon('F-02', 'Tạo quỹ') },
];

/** Màn hình giữ chỗ – module thay bằng màn hình thật (docs/ui-design/screen-specifications.md). */
export const FUND_SETTINGS_ROUTES: Routes = [
  { path: '', ...comingSoon('F-03', 'Thông tin quỹ') },
];
