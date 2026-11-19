import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { SVGIconComponent } from '@progress/kendo-angular-icons';
import { plusIcon } from '@progress/kendo-svg-icons';
import { BehaviorSubject, combineLatest, firstValueFrom, map, switchMap } from 'rxjs';

import { Permissions } from '../../../core/auth/permissions';
import { AppNotifications } from '../../../core/services/notification.service';
import { CurrentFund } from '../../../core/services/current-fund.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { ApiError } from '../../../shared/models/api-error';
import { LOADING, stateKind, toLoadState } from '../../../shared/state/load-state';
import { EventFormDialog } from '../event-form-dialog/event-form-dialog';
import { EventsApi } from '../events.api';
import '../events.errors';
import {
  EVENT_STATUS_BADGE,
  EventSummary,
  MY_RESPONSE_BADGE,
  ParticipationResponse,
  dateBox,
  eventStats,
  isActiveEvent,
} from '../events.models';

type Tab = 'active' | 'done';

interface CardLink {
  text: string;
  link: (string | number)[];
}

/**
 * E-01 · Sự kiện (FR-30, FR-31) theo canvas EventListBody: tab Đang diễn ra / Đã quyết toán – hủy; thẻ có ngày, trạng thái,
 * số liệu. Học sinh trả lời tham gia ngay trên thẻ khi sự kiện còn mở. Thủ quỹ tạo sự kiện (E-02, hộp thoại; `…/events/new`).
 */
@Component({
  selector: 'app-event-list',
  imports: [AppState, ButtonComponent, EventFormDialog, RouterLink, SVGIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <div class="head">
        <div class="head-t">
          <h1>Sự kiện</h1>
          <p class="cf-muted">Mỗi sự kiện có quỹ riêng và quyết toán riêng, không gộp vào quỹ chung.</p>
        </div>
        @if (canManage()) {
          <button kendoButton themeColor="primary" type="button" (click)="creating.set(true)"><kendo-svgicon [icon]="plus" />Tạo sự kiện</button>
        }
      </div>

      @let current = state();
      @if (kind(); as stateKindValue) {
        <app-state [kind]="stateKindValue" />
      } @else if (current.status === 'ready') {
        <div class="tabs" role="tablist" aria-label="Lọc sự kiện">
          <button type="button" role="tab" class="tab" [class.on]="tab() === 'active'" [attr.aria-selected]="tab() === 'active'" (click)="tab.set('active')">
            Đang diễn ra {{ counts().active }}
          </button>
          <button type="button" role="tab" class="tab" [class.on]="tab() === 'done'" [attr.aria-selected]="tab() === 'done'" (click)="tab.set('done')">
            Đã quyết toán / hủy {{ counts().done }}
          </button>
        </div>

        <div class="stack">
          @for (event of visible(); track event.id) {
            @let badge = statusBadge[event.status];
            @let box = date(event.eventDate);
            <article class="cf-card event" [attr.aria-label]="event.name">
              <div class="date">
                @if (box) {
                  <span class="cf-muted xs">{{ box.month }}</span><b>{{ box.day }}</b>
                } @else {
                  <span class="cf-muted xs">–</span>
                }
              </div>
              <div class="row-main">
                <div class="title-row">
                  <a class="name" [routerLink]="['/funds', fundId(), 'events', event.id]">{{ event.name }}</a>
                  <span class="cf-tag" [class]="'cf-tag--' + badge.tone">{{ badge.text }}</span>
                </div>
                @if (event.description) {
                  <span class="cf-muted small">{{ event.description }}</span>
                }
                <span class="cf-muted xs">{{ stats(event) }}</span>
              </div>
              @if (canRespond(event)) {
                @let mine = responseBadge[event.myResponse ?? 'Pending'];
                <div class="rsvp">
                  <span class="cf-tag" [class]="'cf-tag--' + mine.tone">{{ mine.text }}</span>
                  <div class="actions">
                    <button kendoButton fillMode="outline" size="small" type="button" [disabled]="busy()" (click)="respond(event, 'Declined')">Không tham gia</button>
                    <button kendoButton themeColor="primary" size="small" type="button" [disabled]="busy()" (click)="respond(event, 'Accepted')">Tham gia</button>
                  </div>
                </div>
              }
              @let cta = action(event);
              <a class="cf-link-button small-btn" [routerLink]="cta.link">{{ cta.text }}</a>
            </article>
          } @empty {
            <app-state
              kind="empty"
              [title]="tab() === 'active' ? 'Chưa có sự kiện đang diễn ra' : 'Chưa có sự kiện đã quyết toán hoặc hủy'"
              [message]="tab() === 'active' && canManage() ? 'Bấm “Tạo sự kiện” để mời cả lớp tham gia.' : ''"
            />
          }
        </div>
      }
    </div>

    @if (creating()) {
      <app-event-form-dialog [fundId]="fundId()" (closed)="closeNew()" (saved)="onCreated()" />
    }
  `,
  // Theo canvas E-01 (EventListBody.dc.html + cf.css); màu dùng token --cf-*.
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      gap: 24px;
      max-width: var(--cf-content-max);
    }
    .head {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      justify-content: space-between;
      gap: 16px;
    }
    .head-t {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    h1 {
      font-weight: 600;
      letter-spacing: -0.02em;
    }
    p {
      margin: 0;
    }
    .small {
      font-size: 13px;
    }
    .xs {
      font-size: 12px;
    }
    .tabs {
      display: inline-flex;
      flex-wrap: wrap;
      align-self: flex-start;
      gap: 4px;
      padding: 4px;
      border-radius: var(--cf-radius-lg);
      background: var(--cf-subtle);
    }
    .tab {
      display: inline-flex;
      align-items: center;
      height: 28px;
      padding: 0 12px;
      border: 0;
      border-radius: var(--cf-radius-md);
      background: transparent;
      color: var(--cf-text-muted);
      font: inherit;
      font-size: 13px;
      font-weight: 500;
      cursor: pointer;
    }
    .tab.on {
      background: var(--cf-surface);
      color: var(--cf-ink);
      box-shadow: 0 1px 2px rgba(0, 0, 0, 0.08);
    }
    .stack {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .event {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 16px;
      padding: 16px;
    }
    /* Canvas .date: ô tháng / ngày, kẻ phải. */
    .date {
      flex: none;
      width: 48px;
      padding-right: 10px;
      border-right: 1px solid var(--cf-border);
      text-align: center;
    }
    .date b {
      display: block;
      font-size: 20px;
      line-height: 24px;
    }
    .row-main {
      display: flex;
      flex: 1 1 280px;
      flex-direction: column;
      gap: 4px;
      min-width: 0;
    }
    .title-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .name {
      color: var(--cf-ink);
      font-size: 15px;
      font-weight: 600;
      text-decoration: none;
    }
    .name:hover {
      text-decoration: underline;
    }
    .rsvp {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 8px;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    kendo-svgicon {
      margin-right: 2px;
    }
    .small-btn {
      min-height: 32px;
      padding: 0 10px;
      font-size: 13px;
    }
  `,
})
export class EventList {
  private readonly api = inject(EventsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly currentFund = inject(CurrentFund);
  private readonly notifications = inject(AppNotifications);
  private readonly reload$ = new BehaviorSubject<void>(undefined);

  protected readonly plus = plusIcon;
  protected readonly statusBadge = EVENT_STATUS_BADGE;
  protected readonly responseBadge = MY_RESPONSE_BADGE;
  protected readonly date = dateBox;
  protected readonly stats = eventStats;

  protected readonly tab = signal<Tab>('active');
  protected readonly creating = signal(this.route.snapshot.data['openNew'] === true);
  protected readonly busy = signal(false);
  protected readonly canManage = computed(() => this.currentFund.can(Permissions.EventManage));

  protected readonly fundId = toSignal(this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')), { initialValue: '' });

  protected readonly state = toSignal(
    combineLatest([this.route.paramMap, this.reload$]).pipe(switchMap(([params]) => toLoadState(this.api.list(params.get('fundId') ?? '')))),
    { initialValue: LOADING },
  );

  protected readonly kind = computed(() => stateKind(this.state(), () => false));
  private readonly events = computed(() => {
    const current = this.state();
    return current.status === 'ready' ? current.data : [];
  });
  protected readonly counts = computed(() => ({
    active: this.events().filter((event) => isActiveEvent(event.status)).length,
    done: this.events().filter((event) => !isActiveEvent(event.status)).length,
  }));
  protected readonly visible = computed(() => this.events().filter((event) => isActiveEvent(event.status) === (this.tab() === 'active')));

  /** Học sinh (người được mời không quản lý) trả lời ngay trên thẻ khi sự kiện còn mở. */
  protected canRespond(event: EventSummary): boolean {
    return !this.canManage() && event.status === 'Open' && event.myResponse !== null;
  }

  /** Nút bên phải thẻ như canvas: thủ quỹ Quản lý / Quyết toán / Xem báo cáo / Nhật ký; học sinh Chi tiết. */
  protected action(event: EventSummary): CardLink {
    const fund = ['/funds', this.fundId()];
    if (!this.canManage()) {
      return { text: 'Chi tiết', link: [...fund, 'events', event.id] };
    }
    switch (event.status) {
      case 'Completed':
        return { text: 'Quyết toán', link: [...fund, 'events', event.id, 'settlement'] };
      case 'Settled':
        return { text: 'Xem báo cáo', link: [...fund, 'reports'] };
      case 'Cancelled':
        return { text: 'Nhật ký', link: [...fund, 'audit'] };
      default:
        return { text: 'Quản lý', link: [...fund, 'events', event.id] };
    }
  }

  protected async respond(event: EventSummary, response: Exclude<ParticipationResponse, 'Pending'>): Promise<void> {
    this.busy.set(true);
    try {
      await firstValueFrom(this.api.respond(this.fundId(), event.id, response));
      this.notifications.success(
        response === 'Accepted'
          ? 'Đã xác nhận tham gia. Khoản thu sự kiện sẽ hiện ở “Khoản cần đóng”.'
          : 'Đã ghi nhận không tham gia – bạn sẽ không phải đóng tiền sự kiện này.',
      );
      this.reload$.next();
    } catch (error) {
      this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
    } finally {
      this.busy.set(false);
    }
  }

  protected closeNew(): void {
    this.creating.set(false);
    if (this.route.snapshot.data['openNew']) {
      void this.router.navigate(['..'], { relativeTo: this.route });
    }
  }

  protected onCreated(): void {
    this.closeNew();
    this.tab.set('active');
    this.reload$.next();
  }
}
