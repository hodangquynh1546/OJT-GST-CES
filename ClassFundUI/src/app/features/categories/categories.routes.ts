import { Routes } from '@angular/router';

/** C-01 Danh mục thu chi (guard Category.Manage ở app.routes.ts). */
export const CATEGORY_ROUTES: Routes = [
  { path: '', title: 'Danh mục thu chi', loadComponent: () => import('./category-list/category-list').then((m) => m.CategoryList) },
];
