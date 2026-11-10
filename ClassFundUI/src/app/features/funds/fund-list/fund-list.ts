import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { filter } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { Permissions } from '../../../core/auth/permissions';
import { AppNotifications } from '../../../core/services/notification.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { VnDatePipe } from '../../../shared/pipes/vn-date.pipe';
import { VndMoneyPipe } from '../../../shared/pipes/vnd-money.pipe';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { CreateFundDialog, CreatedFund } from '../create-fund-dialog/create-fund-dialog';
import { FundsApi } from '../funds.api';
import { FundSummary } from '../funds.models';

interface FundCard {
  fund: FundSummary;
  link: string;
  /** Màu viền trên của thẻ (Canvas G-01): đã có thủ quỹ / chưa phân công / quỹ của con. */
  tone: 'assigned' | 'unassigned' | 'child';
  status: string;
  facts: { label: string; value: string }[];
  cta: string;
}

/**
 * G-01 "Quỹ của tôi" (GVCN) / "Quỹ của con" (phụ huynh) – Canvas board `FundList`, `FundListParent`; FR-22, FR-23.
 * GVCN: quỹ chưa phân công → G-03, quỹ có thủ quỹ → S-01. Phụ huynh: mở bản công khai (S-01).
 */
@Component({
  selector: 'app-fund-list',
  imports: [RouterLink, ButtonComponent, AppState, PageHeader, CreateFundDialog, VndMoneyPipe],
  providers: [VnDatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <app-page-header [title]="title()" [subtitle]="subtitle()">
        @if (canCreate()) {
          <button kendoButton themeColor="primary" type="button" (click)="openCreate()">+ Tạo quỹ</button>
        }
      </app-page-header>

      @switch (kind()) {
        @case (null) {
          <div class="grid">
            @for (card of cards(); track card.fund.id) {
              <a class="cf-card fund" [class]="'fund--' + card.tone" [routerLink]="card.link">
                <div class="head">
                  <b class="name">{{ card.fund.name }}</b>
                  <span class="cf-tag" [class.cf-tag--success]="card.tone !== 'unassigned'"
                    [class.cf-tag--warning]="card.tone === 'unassigned'">{{ card.status }}</span>
                </div>
                <div class="row">
                  <span class="cf-muted">Số dư hiện tại</span>
                  <b class="cf-money" [class.cf-expense]="card.fund.currentBalance < 0">{{
                    card.fund.currentBalance | vndMoney
                  }}</b>
                </div>
                @for (fact of card.facts; track fact.label) {
                  <div class="row small">
                    <span class="cf-muted">{{ fact.label }}</span>
                    <span class="value">{{ fact.value }}</span>
                  </div>
                }
                <span class="cta">{{ card.cta }} →</span>
              </a>
            }
          </div>
        }
        @case ('empty') {
          @if (isTeacher()) {
            <app-state
              class="cf-card"
              kind="empty"
              title="Bạn chưa có quỹ nào"
              message="Tạo quỹ cho lớp chủ nhiệm. Quỹ mới có sẵn danh mục thu chi mặc định và ở trạng thái “Chưa phân công thủ quỹ”."
              actionLabel="+ Tạo quỹ đầu tiên"
              (action)="openCreate()"
            />
          } @else {
            <app-state class="cf-card" kind="empty" title="Chưa có quỹ nào" />
          }
        }
        @case ('error') {
          <app-state class="cf-card" kind="error" actionLabel="Thử lại" (action)="load()" />
        }
        @default {
          <app-state [kind]="kind()!" />
        }
      }

      @if (isParent()) {
        <span class="cf-muted note">
          Mỗi quỹ chỉ hiện dữ liệu công khai. Con bạn có làm thủ quỹ thì tài khoản phụ huynh của bạn cũng không có
          quyền thủ quỹ.
        </span>
      }
    </section>

    @if (creating()) {
      <app-create-fund-dialog (created)="onCreated($event)" (closed)="creating.set(false)" />
    }
  `,
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      gap: 16px;
      max-width: 1100px;
      margin: 0 auto;
    }
    app-page-header {
      margin-bottom: 0;
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
      gap: 16px;
    }
    .fund {
      display: flex;
      flex-direction: column;
      gap: 10px;
      border-top-width: 3px;
      color: var(--cf-text);
      text-decoration: none;
    }
    .fund:hover {
      border-color: var(--cf-border-strong);
      color: var(--cf-text);
    }
    .fund--assigned,
    .fund--assigned:hover {
      border-top-color: var(--cf-primary);
    }
    .fund--unassigned,
    .fund--unassigned:hover {
      border-top-color: var(--cf-warning-fg);
    }
    .fund--child,
    .fund--child:hover {
      border-top-color: var(--cf-income);
    }
    .head {
      display: flex;
      align-items: flex-start;
      gap: 8px;
    }
    .name {
      flex: 1;
      min-width: 0;
      font-size: 16px;
      overflow-wrap: break-word;
    }
    .cf-tag {
      height: 22px;
    }
    .row {
      display: flex;
      justify-content: space-between;
      gap: 8px;
    }
    .small {
      font-size: 13px;
    }
    .value {
      text-align: right;
    }
    .cta {
      color: var(--cf-primary);
      font-size: 13px;
      font-weight: 600;
    }
    .note {
      font-size: 13px;
    }
  `,
})
export class FundList {
  private readonly api = inject(FundsApi);
  private readonly auth = inject(AuthService);
  private readonly notifications = inject(AppNotifications);
  private readonly date = inject(VnDatePipe);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly state = signal<LoadState<FundSummary[]>>(LOADING);
  protected readonly creating = signal(false);

  protected readonly isParent = computed(() => this.auth.user()?.kind === 'Parent');
  protected readonly isTeacher = computed(() => this.auth.user()?.kind === 'Teacher');
  protected readonly canCreate = computed(
    () => this.auth.user()?.accountPermissions.includes(Permissions.FundCreate) ?? false,
  );
  protected readonly kind = computed(() => stateKind(this.state()));
  protected readonly title = computed(() => (this.isParent() ? 'Quỹ của con' : 'Quỹ của tôi'));
  protected readonly subtitle = computed(() => {
    const state = this.state();
    const count = state.status === 'ready' ? ` · ${state.data.length} quỹ` : '';
    if (this.isParent()) {
      return `Các quỹ có con bạn được giáo viên liên kết${count}`;
    }
    return this.isTeacher() ? `Các quỹ bạn là giáo viên chủ nhiệm${count}` : `Các quỹ bạn tham gia${count}`;
  });
  protected readonly cards = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data.map((fund) => this.toCard(fund)) : [];
  });

  constructor() {
    this.load();
  }

  /** Tải danh sách; `silent` giữ dữ liệu cũ trên màn hình trong lúc tải lại (sau khi tạo quỹ). */
  protected load(silent = false): void {
    toLoadState(this.api.list())
      .pipe(
        filter((state) => !silent || state.status !== 'loading'),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((state) => this.state.set(state));
  }

  protected openCreate(): void {
    this.creating.set(true);
  }

  protected async onCreated(fund: CreatedFund): Promise<void> {
    this.creating.set(false);
    this.notifications.success(`Đã tạo “${fund.name}”. Bước tiếp: thêm học sinh rồi phân công thủ quỹ.`);
    this.load(true);
    // Quỹ mới có trong memberships của /auth/me → header, menu và guard theo quỹ nhận quỹ mới.
    await this.auth.restoreSession();
  }

  private toCard(fund: FundSummary): FundCard {
    const treasurers = fund.treasurerNames.length > 0 ? fund.treasurerNames.join(', ') : '—';
    if (fund.role === 'Parent') {
      return {
        fund,
        link: `/funds/${fund.id}`,
        tone: 'child',
        status: `Con: ${(fund.linkedStudents ?? []).join(', ')}`,
        facts: [{ label: 'Thủ quỹ', value: treasurers }],
        cta: 'Xem bản công khai',
      };
    }

    const unassigned = fund.treasurerStatus === 'Unassigned';
    const teacher = fund.role === 'Teacher';
    const facts = teacher ? [{ label: 'Học sinh', value: `${fund.studentCount ?? 0} học sinh` }] : [];
    facts.push({ label: 'Thủ quỹ', value: treasurers });
    if (teacher) {
      facts.push({ label: 'Ngày tạo', value: this.date.transform(fund.createdAt) });
    }
    return {
      fund,
      link: teacher && unassigned ? `/funds/${fund.id}/treasurers` : `/funds/${fund.id}`,
      tone: unassigned ? 'unassigned' : 'assigned',
      status: unassigned ? 'Chưa phân công thủ quỹ' : 'Đã có thủ quỹ',
      facts,
      cta: teacher && unassigned ? 'Phân công thủ quỹ' : 'Mở quỹ',
    };
  }
}
