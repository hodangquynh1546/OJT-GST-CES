import { Routes } from '@angular/router';

import './categories.errors';

/** Module Danh mục (M5) – T-02 tại `/funds/:fundId/categories` (guard Category.Manage ở app.routes.ts). */
export const CATEGORY_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./categories-page/categories-page').then((m) => m.CategoriesPage) },
];
