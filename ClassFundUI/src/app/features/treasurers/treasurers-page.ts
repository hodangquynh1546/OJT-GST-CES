import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { ComboBoxComponent } from '@progress/kendo-angular-dropdowns';
import { Subject, debounceTime, firstValueFrom, map, merge, of, startWith, switchMap } from 'rxjs';

import { ROLE_DISPLAY } from '../../core/navigation/role-display';
import { ConfirmDialog } from '../../core/services/confirm-dialog.service';
import { AppNotifications } from '../../core/services/notification.service';
import { AppState } from '../../shared/components/app-state/app-state';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { VnDatePipe } from '../../shared/pipes/vn-date.pipe';
import { ApiError } from '../../shared/models/api-error';
import { initialsOf } from '../../shared/utils/initials';
import { LOADING, LoadState, stateKind, toLoadState } from '../../shared/state/load-state';
import { Candidate, Treasurer } from './treasurers.models';
import { TreasurersApi } from './treasurers.api';

/** T-01 Thủ quỹ (FR-12, FR-13): chủ quỹ phân quyền một học sinh làm thủ quỹ và thu hồi. */
@Component({
  selector: 'app-treasurers-page',
  imports: [ButtonComponent, ComboBoxComponent, PageHeader, AppState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <app-page-header
        title="Thủ quỹ"
        subtitle="Chỉ thủ quỹ chính phân quyền / thu hồi. Thủ quỹ được phân quyền không thay đổi được quyền của thủ quỹ khác."
      />

      <section class="cf-card grant" aria-labelledby="grant-title">
        <label id="grant-title" class="label" for="candidate">Chọn học sinh trong quỹ</label>
        <div class="grant-row">
          <kendo-combobox
            #combo
            class="combo"
            [data]="candidates()"
            textField="label"
            valueField="memberId"
            [valuePrimitive]="true"
            [filterable]="true"
            placeholder="Gõ tên hoặc email để tìm"
            [value]="selected()"
            [inputAttributes]="{ id: 'candidate' }"
            (focus)="combo.toggle(true)"
            (filterChange)="filter.next($event)"
            (valueChange)="selected.set($event)"
          />
          <button kendoButton themeColor="primary" type="button" [disabled]="!selected() || busy()" (click)="grant()">
            {{ busy() ? 'Đang phân quyền…' : 'Phân quyền thủ quỹ' }}
          </button>
        </div>
        @if (noCandidates()) {
          <p class="cf-muted hint">Chưa có học sinh nào để phân quyền. Hãy thêm học sinh vào quỹ trước.</p>
        }
      </section>

      @if (kind(); as value) {
        <app-state [kind]="value" />
      } @else {
        <section class="cf-card team" aria-labelledby="team-title">
          <h2 id="team-title">Đội thủ quỹ</h2>
          <ul class="rows">
            @for (person of team(); track person.memberId) {
              <li class="row">
                <span class="avatar" aria-hidden="true">{{ initials(person.fullName) }}</span>
                <span class="who">
                  <b>{{ person.fullName }}</b>
                  <span class="cf-muted">{{ subtitle(person) }}</span>
                </span>
                <span [class]="'role role--' + person.role">{{ roleLabel(person) }}</span>
                @if (person.role === 'Treasurer') {
                  <button kendoButton fillMode="outline" themeColor="error" type="button" [disabled]="busy()" [attr.aria-label]="'Thu hồi ' + person.fullName" (click)="revoke(person)">
                    Thu hồi
                  </button>
                }
              </li>
            }
          </ul>
        </section>
      }

      <section class="cf-card info" aria-labelledby="perm-title">
        <h2 id="perm-title">Quyền của thủ quỹ</h2>
        <p class="cf-muted">
          Tạo khoản thu, xác nhận/từ chối thanh toán, ghi thu chi, quản lý sự kiện, quyết toán, xử lý rút tiền, quản lý
          học sinh &amp; danh mục, xem nhật ký và báo cáo. Thủ quỹ vẫn là thành viên: phải đóng tiền như học sinh và được
          tự ghi nhận (gắn nhãn “Tự ghi nhận”).
        </p>
      </section>
    </div>
  `,
  styles: `
    .page {
      max-width: 912px;
    }
    .grant,
    .team,
    .info {
      margin-bottom: 16px;
    }
    .label {
      display: block;
      margin-bottom: 8px;
      font-weight: 500;
    }
    .grant-row {
      display: flex;
      gap: 12px;
    }
    .combo {
      flex: 1;
      min-width: 0;
    }
    .hint {
      margin: 8px 0 0;
    }
    h2 {
      margin: 0 0 12px;
      font-size: 16px;
      line-height: 24px;
    }
    .rows {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .row {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 12px 0;
      border-top: 1px solid var(--cf-border);
    }
    .row:first-child {
      border-top: 0;
    }
    .avatar {
      display: flex;
      flex: none;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      border-radius: 16px;
      background: var(--cf-subtle);
      font-size: 12px;
      font-weight: 700;
    }
    .who {
      display: flex;
      flex: 1;
      flex-direction: column;
      min-width: 0;
    }
    .role {
      display: inline-block;
      padding: 0 10px;
      border-radius: var(--cf-radius-pill);
      font-size: 12px;
      font-weight: 600;
      line-height: 24px;
    }
    .role--Owner {
      background: var(--cf-ink);
      color: var(--cf-surface);
    }
    .role--Treasurer {
      background: var(--cf-role-treasurer-tint);
      color: var(--cf-role-treasurer);
    }
    .info p {
      margin: 0;
    }
  `,
})
export class TreasurersPage {
  private readonly api = inject(TreasurersApi);
  private readonly confirm = inject(ConfirmDialog);
  private readonly notifications = inject(AppNotifications);

  readonly fundId = input.required<string>();

  protected readonly refresh = signal(0);
  protected readonly busy = signal(false);
  protected readonly selected = signal<string | null>(null);
  protected readonly filter = new Subject<string>();

  // ---------- Đội thủ quỹ ----------

  private readonly state = toSignal(
    toObservable(computed(() => ({ fundId: this.fundId(), refresh: this.refresh() }))).pipe(
      switchMap(({ fundId }) => toLoadState(this.api.list(fundId))),
    ),
    { initialValue: LOADING as LoadState<Treasurer[]> },
  );
  private lastTeam: Treasurer[] = [];
  protected readonly team = computed(() => {
    const state = this.state();
    if (state.status === 'ready') {
      this.lastTeam = state.data;
    }
    return this.lastTeam;
  });
  protected readonly kind = computed(() => {
    const state = this.state();
    return state.status === 'error' || (state.status === 'loading' && this.lastTeam.length === 0) ? stateKind(state) : null;
  });

  // ---------- Học sinh để chọn (lọc theo lúc gõ) ----------

  private readonly search = toSignal(
    merge(of(''), this.filter.pipe(debounceTime(250), map((text) => text.trim()))).pipe(startWith('')),
    { initialValue: '' },
  );
  protected readonly candidates = toSignal(
    toObservable(computed(() => ({ fundId: this.fundId(), search: this.search(), refresh: this.refresh() }))).pipe(
      switchMap(({ fundId, search }) => this.api.candidates(fundId, search)),
    ),
    { initialValue: [] as Candidate[] },
  );
  protected readonly noCandidates = computed(() => this.candidates().length === 0 && this.search() === '');

  // ---------- Hiển thị ----------

  protected initials(fullName: string): string {
    return initialsOf(fullName);
  }

  protected roleLabel(person: Treasurer): string {
    return ROLE_DISPLAY[person.role].badge;
  }

  protected subtitle(person: Treasurer): string {
    if (person.role === 'Owner') {
      return 'Người tạo quỹ · không thể thu hồi';
    }
    return person.grantedAt ? `Phân quyền ${this.formatDate(person.grantedAt)}` : 'Thủ quỹ được phân quyền';
  }

  private formatDate(value: string): string {
    return new VnDatePipe().transform(value);
  }

  // ---------- Hành động ----------

  protected async grant(): Promise<void> {
    const memberId = this.selected();
    if (!memberId || this.busy()) {
      return;
    }

    this.busy.set(true);
    try {
      const person = await firstValueFrom(this.api.grant(this.fundId(), memberId));
      this.selected.set(null);
      this.refresh.update((value) => value + 1);
      this.notifications.success(`Đã phân quyền thủ quỹ cho ${person.fullName}.`);
    } catch (error) {
      this.notifyError(error);
      this.refresh.update((value) => value + 1);
    } finally {
      this.busy.set(false);
    }
  }

  protected revoke(person: Treasurer): void {
    this.confirm
      .confirm({
        title: 'Thu hồi quyền thủ quỹ',
        message: `Thu hồi quyền thủ quỹ của ${person.fullName}? Họ trở lại là học sinh của quỹ và mất quyền quản lý ngay lập tức.`,
        confirmLabel: 'Thu hồi',
        danger: true,
      })
      .subscribe(async (confirmed) => {
        if (!confirmed) {
          return;
        }
        this.busy.set(true);
        try {
          await firstValueFrom(this.api.revoke(this.fundId(), person.memberId));
          this.refresh.update((value) => value + 1);
          this.notifications.success(`Đã thu hồi quyền thủ quỹ của ${person.fullName}.`);
        } catch (error) {
          this.notifyError(error);
          this.refresh.update((value) => value + 1);
        } finally {
          this.busy.set(false);
        }
      });
  }

  private notifyError(error: unknown): void {
    this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
  }
}
