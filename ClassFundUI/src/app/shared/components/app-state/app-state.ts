import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { SkeletonComponent } from '@progress/kendo-angular-indicators';

export type AppStateKind =
  | 'loading'
  | 'empty'
  | 'empty-filter'
  | 'error'
  | 'forbidden'
  | 'not-found'
  | 'warning';

interface StateText {
  title: string;
  message: string;
}

/** Nội dung mặc định theo artboard States trên Canvas. */
const DEFAULT_TEXT: Record<Exclude<AppStateKind, 'loading'>, StateText> = {
  empty: { title: 'Chưa có dữ liệu', message: '' },
  'empty-filter': {
    title: 'Không có giao dịch phù hợp',
    message: 'Thử bỏ bớt điều kiện lọc hoặc đổi tháng.',
  },
  error: {
    title: 'Không tải được dữ liệu',
    message: 'Kết nối bị gián đoạn. Dữ liệu của bạn không bị thay đổi.',
  },
  forbidden: {
    title: 'Trang này không dành cho vai trò của bạn',
    message: 'Bạn vẫn xem được các mục có trong menu.',
  },
  'not-found': {
    title: 'Không tìm thấy trang',
    message: 'Trang không tồn tại hoặc không thuộc quỹ của bạn.',
  },
  warning: { title: '', message: '' },
};

/** Trạng thái dùng chung. Loading giữ chiều cao để bố cục không nhảy. */
@Component({
  selector: 'app-state',
  imports: [ButtonComponent, SkeletonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (kind() === 'loading') {
      <div class="loading" role="status" aria-label="Đang tải">
        <kendo-skeleton shape="text" width="40%" />
        <kendo-skeleton shape="rectangle" width="100%" height="96px" />
      </div>
    } @else {
      <div class="state" [class.alert]="isAlert()" [attr.role]="isAlert() ? 'alert' : null">
        @if (code(); as value) {
          <span class="code">{{ value }}</span>
        }
        @if (heading(); as text) {
          <b class="title">{{ text }}</b>
        }
        @if (body(); as text) {
          <span class="cf-muted">{{ text }}</span>
        }
        @if (actionLabel(); as label) {
          <button kendoButton themeColor="primary" type="button" (click)="action.emit()">
            {{ label }}
          </button>
        }
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .loading {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .state {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      padding: 32px 16px;
      text-align: center;
    }
    .code {
      color: var(--cf-text-muted);
      font-size: 34px;
      font-weight: 700;
      line-height: 42px;
    }
    .title {
      font-size: 16px;
    }
    button {
      margin-top: 8px;
    }
  `,
})
export class AppState {
  readonly kind = input.required<AppStateKind>();
  readonly title = input<string>();
  readonly message = input<string>();
  readonly actionLabel = input<string>();
  readonly action = output<void>();

  private readonly defaults = computed(() => {
    const kind = this.kind();
    return kind === 'loading' ? null : DEFAULT_TEXT[kind];
  });

  protected readonly heading = computed(() => this.title() ?? this.defaults()?.title);
  protected readonly body = computed(() => this.message() ?? this.defaults()?.message);
  protected readonly isAlert = computed(() => this.kind() === 'error' || this.kind() === 'warning');
  protected readonly code = computed(() => {
    const kind = this.kind();
    return kind === 'forbidden' ? '403' : kind === 'not-found' ? '404' : null;
  });
}
