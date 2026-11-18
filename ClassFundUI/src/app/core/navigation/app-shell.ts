import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { AppHeader } from './app-header';
import { AppNav } from './app-nav';

/**
 * Khung trang trong một quỹ (canvas S-01): sidebar bên trái (thẻ quỹ + menu theo nhóm), bên phải là thanh trên
 * và vùng nội dung. Chỉ vào khung này sau khi chọn quỹ ở "Quỹ của tôi".
 */
@Component({
  selector: 'app-shell',
  imports: [AppHeader, AppNav, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-nav class="nav" />
    <div class="main">
      <app-header />
      <main class="content"><router-outlet /></main>
    </div>
  `,
  styles: `
    :host {
      display: flex;
      height: 100dvh;
    }
    .nav {
      flex: none;
    }
    .main {
      display: flex;
      flex: 1;
      flex-direction: column;
      min-width: 0;
    }
    .content {
      flex: 1;
      /* Mốc định vị cho phần tử absolute bên trong (ví dụ .sr-only): nếu không, chúng thoát khỏi vùng cuộn này,
         làm cả trang cuộn thêm và kéo thanh trên + menu lên. */
      position: relative;
      overflow: auto;
      box-sizing: border-box;
      min-width: 0;
      padding: 24px;
    }
  `,
})
export class AppShell {}
