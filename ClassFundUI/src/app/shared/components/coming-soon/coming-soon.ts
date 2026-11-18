import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Route } from '@angular/router';

import { PageHeader } from '../page-header/page-header';

/** Trang giữ chỗ cho màn hình chưa triển khai; mỗi feature thay bằng màn hình thật. */
@Component({
  selector: 'app-coming-soon',
  imports: [PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header [title]="title()" />
    <p class="cf-card cf-muted">Chức năng này đang được phát triển.</p>
  `,
})
export class ComingSoon {
  readonly title = input.required<string>();
  /** Mã màn hình trong tài liệu thiết kế (S-01, M-01…): chỉ để tra cứu, không hiển thị cho người dùng. */
  readonly screenCode = input.required<string>();
}

/** Khai báo route giữ chỗ; title và screenCode được gán vào input qua component input binding. */
export function comingSoon(screenCode: string, title: string): Pick<Route, 'component' | 'data'> {
  return { component: ComingSoon, data: { screenCode, title } };
}
