import { Routes } from '@angular/router';

/** Module Học sinh & Phụ huynh (M3) – G-02 tại `/funds/:fundId/students` (guard Student.Manage ở app.routes.ts). */
export const STUDENT_ROUTES: Routes = [
  { path: '', title: 'Học sinh & phụ huynh', loadComponent: () => import('./students-page/students-page').then((m) => m.StudentsPage) },
];
