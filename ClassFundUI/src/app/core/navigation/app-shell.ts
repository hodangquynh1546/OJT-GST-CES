import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { AppHeader } from './app-header';
import { AppNav } from './app-nav';

/** Khung trang sau đăng nhập: header, menu theo vai trò và vùng nội dung. */
@Component({
  selector: 'app-shell',
  imports: [AppHeader, AppNav, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-header />
    <div class="body">
      <app-nav class="nav" />
      <main class="content"><router-outlet /></main>
    </div>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      height: 100dvh;
    }
    .body {
      display: flex;
      flex: 1;
      min-height: 0;
    }
    .nav {
      flex: none;
    }
    .content {
      flex: 1;
      /* Mốc định vị cho phần tử absolute bên trong (ví dụ .sr-only): nếu không, chúng thoát khỏi vùng cuộn này,
         làm cả trang cuộn thêm và kéo header + menu lên. */
      position: relative;
      overflow: auto;
      box-sizing: border-box;
      min-width: 0;
      padding: 24px;
    }
  `,
})
export class AppShell {}
