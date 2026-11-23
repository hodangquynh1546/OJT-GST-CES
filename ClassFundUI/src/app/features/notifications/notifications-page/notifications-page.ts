import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { firstValueFrom, map } from 'rxjs';

import { AppState } from '../../../shared/components/app-state/app-state';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { ScopeBadge } from '../../../shared/components/scope-badge/scope-badge';
import { AppNotifications } from '../../../core/services/notification.service';
import { ApiError } from '../../../shared/models/api-error';
import { NotificationsApi } from '../notifications.api';
import {
  AppNotification,
  NOTIFICATION_PAGE_SIZE,
  NotificationList,
  notificationLink,
  splitTitle,
} from '../notifications.models';
import { NotificationsStore } from '../notifications.store';

type Tab = 'all' | 'unread';

const dateTime = new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

/**
 * N-01 · Thông báo (FR-43, FR-44): mỗi thông báo ghi rõ phạm vi Quỹ chung / Sự kiện; xem danh sách, lọc chưa đọc,
 * đánh dấu đã đọc (bấm vào để mở nơi liên quan) và đánh dấu tất cả đã đọc.
 */
@Component({
  selector: 'app-notifications-page',
  imports: [AppState, ButtonComponent, PageHeader, ScopeBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <app-page-header title="Thông báo" subtitle="Cập nhật về khoản thu, sự kiện, thanh toán và số dư của bạn.">
        <button kendoButton fillMode="outline" type="button" [disabled]="busy() || unread() === 0" (click)="markAll()">Đánh dấu tất cả đã đọc</button>
      </app-page-header>

      <div class="tabs" role="tablist" aria-label="Lọc thông báo">
        <button type="button" role="tab" [class.active]="tab() === 'all'" [attr.aria-selected]="tab() === 'all'" (click)="setTab('all')">Tất cả</button>
        <button type="button" role="tab" [class.active]="tab() === 'unread'" [attr.aria-selected]="tab() === 'unread'" (click)="setTab('unread')">
          Chưa đọc {{ unread() }}
        </button>
      </div>

      @if (error(); as message) {
        <app-state kind="error" [message]="message" />
      } @else if (loading()) {
        <app-state kind="loading" />
      } @else if (items().length === 0) {
        <app-state kind="empty" [title]="tab() === 'unread' ? 'Không còn thông báo chưa đọc' : 'Chưa có thông báo nào'" />
      } @else {
        <ul class="cf-card list" aria-label="Danh sách thông báo">
          @for (item of items(); track item.id) {
            @let parts = split(item.title);
            <li>
              <button type="button" class="item" [class.unread]="!item.isRead" (click)="open(item)">
                <span class="dot" [attr.aria-label]="item.isRead ? null : 'Chưa đọc'" [class.on]="!item.isRead"></span>
                <span class="main">
                  <span class="head">
                    <app-scope-badge [scope]="item.scope" [eventName]="parts.eventName" />
                    <b class="title">{{ parts.text }}</b>
                  </span>
                  <span class="cf-muted body">{{ item.body }}</span>
                </span>
                <span class="cf-muted time">{{ time(item.createdAt) }}</span>
              </button>
            </li>
          }
        </ul>
        <div class="pager">
          <span class="cf-muted small">Hiển thị {{ items().length }} / {{ total() }} thông báo</span>
          <div class="actions">
            <button kendoButton fillMode="outline" size="small" type="button" [disabled]="page() <= 1 || loading()" (click)="go(-1)">Trước</button>
            <button kendoButton fillMode="outline" size="small" type="button" [disabled]="page() >= pageCount() || loading()" (click)="go(1)">Sau</button>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .page { display: flex; flex-direction: column; gap: 16px; max-width: 900px; }
    .tabs { display: inline-flex; align-self: flex-start; gap: 4px; padding: 4px; border-radius: var(--cf-radius-lg); background: var(--cf-subtle); }
    .tabs button { height: 28px; padding: 0 12px; border: 0; border-radius: var(--cf-radius-md); background: transparent; color: var(--cf-text-muted); font: inherit; font-size: 13px; font-weight: 500; cursor: pointer; }
    .tabs button.active { background: var(--cf-surface); color: var(--cf-text); box-shadow: 0 1px 2px rgba(15, 23, 42, 0.1); }
    .list { margin: 0; padding: 0; list-style: none; }
    .item { display: flex; align-items: flex-start; gap: 12px; width: 100%; padding: 14px 16px; border: 0; border-top: 1px solid var(--cf-border); background: transparent; color: inherit; font: inherit; text-align: left; cursor: pointer; }
    li:first-child .item { border-top: 0; }
    .item:hover { background: var(--cf-background); }
    .item.unread { background: var(--cf-primary-tint); }
    .dot { flex: none; width: 8px; height: 8px; margin-top: 8px; border-radius: 4px; background: transparent; }
    .dot.on { background: var(--cf-primary); }
    .main { display: flex; flex: 1; flex-direction: column; gap: 4px; min-width: 0; }
    .head { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
    .body { font-size: 13px; }
    .time { flex: none; font-size: 12px; white-space: nowrap; }
    .item:not(.unread) .title { font-weight: 500; }
    .pager { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
    .actions { display: flex; gap: 8px; }
  `,
})
export class NotificationsPage {
  private readonly api = inject(NotificationsApi);
  private readonly store = inject(NotificationsStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notifications = inject(AppNotifications);

  protected readonly split = splitTitle;
  protected readonly fundId = toSignal(this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')), { initialValue: '' });

  protected readonly tab = signal<Tab>('all');
  protected readonly page = signal(1);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly data = signal<NotificationList | null>(null);

  protected readonly items = computed(() => this.data()?.items ?? []);
  protected readonly total = computed(() => this.data()?.total ?? 0);
  protected readonly unread = computed(() => this.data()?.unreadCount ?? 0);
  protected readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / NOTIFICATION_PAGE_SIZE)));

  constructor() {
    void this.load();
  }

  protected time(value: string): string {
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? '' : dateTime.format(parsed);
  }

  protected setTab(tab: Tab): void {
    this.tab.set(tab);
    this.page.set(1);
    void this.load();
  }

  protected go(step: number): void {
    this.page.update((value) => value + step);
    void this.load();
  }

  private currentFundId(): string {
    return this.fundId() || this.route.snapshot.paramMap.get('fundId') || '';
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await firstValueFrom(
        this.api.list(this.currentFundId(), { unreadOnly: this.tab() === 'unread', page: this.page(), pageSize: NOTIFICATION_PAGE_SIZE }),
      );
      this.data.set(result);
      this.store.set(result.unreadCount);
    } catch (error) {
      this.error.set(error instanceof ApiError ? error.message : 'Không tải được thông báo. Vui lòng thử lại.');
    } finally {
      this.loading.set(false);
    }
  }

  protected async open(item: AppNotification): Promise<void> {
    const fundId = this.currentFundId();
    if (!item.isRead) {
      try {
        await firstValueFrom(this.api.markRead(fundId, item.id));
        this.store.set(this.store.unread() - 1);
      } catch {
        // Vẫn mở nơi liên quan; trạng thái đọc sẽ đồng bộ ở lần tải sau.
      }
    }
    await this.router.navigate(notificationLink(fundId, item));
  }

  protected async markAll(): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    try {
      const { marked } = await firstValueFrom(this.api.markAllRead(this.currentFundId()));
      this.store.set(0);
      this.notifications.success(marked > 0 ? `Đã đánh dấu ${marked} thông báo là đã đọc.` : 'Không có thông báo chưa đọc.');
      await this.load();
    } catch (error) {
      this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
    } finally {
      this.busy.set(false);
    }
  }
}
