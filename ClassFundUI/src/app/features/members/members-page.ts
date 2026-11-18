import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { CellTemplateDirective, ColumnComponent, GridComponent, NoRecordsTemplateDirective, PageChangeEvent } from '@progress/kendo-angular-grid';
import { debounceTime, distinctUntilChanged, firstValueFrom, map, switchMap } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';
import { ROLE_DISPLAY } from '../../core/navigation/role-display';
import { ConfirmDialog } from '../../core/services/confirm-dialog.service';
import { AppNotifications } from '../../core/services/notification.service';
import { AppState } from '../../shared/components/app-state/app-state';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { applyServerErrors } from '../../shared/forms/server-errors';
import { VnDatePipe } from '../../shared/pipes/vn-date.pipe';
import { LoadState, LOADING, stateKind, toLoadState } from '../../shared/state/load-state';
import { ApiError } from '../../shared/models/api-error';
import { PagedResult } from '../../shared/models/paged-result';
import { AuthField } from '../auth/auth-field';
import { emailFormatValidator } from '../auth/auth-validators';
import { MEMBER_FIELD_FOR } from './members.errors';
import { MembersApi } from './members.api';
import {
  AddMemberResult,
  MAX_BULK_EMAILS,
  Member,
  MemberStatus,
  OUTCOME_LABELS,
  RemovalPreview,
} from './members.models';
import { countInvalidEmails, credentialsText, parseEmails, uniqueEmails } from './members.utils';

const PAGE_SIZE = 20;
const NOT_MANAGED_HINT = 'Tài khoản này do chính họ quản lý (tự đăng ký hoặc đang ở quỹ khác).';

interface CredentialRow {
  email: string;
  password: string;
}

/** M-01 Học sinh (FR-09 → FR-11): thêm bằng email (một hoặc nhiều), cấp lại mật khẩu tạm, sửa tên, ngừng. */
@Component({
  selector: 'app-members-page',
  imports: [
    ReactiveFormsModule,
    ButtonComponent,
    DialogComponent,
    DialogActionsComponent,
    GridComponent,
    ColumnComponent,
    CellTemplateDirective,
    NoRecordsTemplateDirective,
    PageHeader,
    AppState,
    AuthField,
    VnDatePipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-page">
      <app-page-header
        title="Học sinh"
        [subtitle]="activeCount() + ' thành viên đang hoạt động. Thêm bằng email: email chưa có tài khoản thì hệ thống tạo tài khoản kèm mật khẩu tạm.'"
      />

      @if (fundArchived()) {
        <p class="cf-alert cf-alert--warning archived" role="status">
          Quỹ đã tổng kết cuối năm, chỉ được xem. Không thể thêm thành viên mới.
        </p>
      } @else {
        <section class="cf-card add" aria-labelledby="add-title">
          <h2 id="add-title" class="sr-only">Thêm học sinh</h2>
          <form [formGroup]="addForm" (ngSubmit)="addOne()" novalidate class="add-form">
            <app-auth-field fieldId="member-email" label="Email học sinh" type="email" [control]="addForm.controls.email" />
            <app-auth-field fieldId="member-name" label="Họ tên" [maxLength]="100" [control]="addForm.controls.fullName" />
            <div class="add-actions">
              <button kendoButton type="submit" themeColor="primary" [disabled]="adding()">
                {{ adding() ? 'Đang thêm…' : 'Thêm vào lớp' }}
              </button>
              <button kendoButton type="button" fillMode="outline" (click)="openBulk()">Thêm nhiều email</button>
            </div>
          </form>
          @if (addError(); as message) {
            <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
          }
        </section>
      }

      <section class="cf-card list" aria-labelledby="list-title">
        <h2 id="list-title" class="sr-only">Danh sách thành viên</h2>
        <div class="toolbar">
          <div class="tabs" role="tablist" aria-label="Trạng thái thành viên">
            <button type="button" role="tab" [attr.aria-selected]="status() === 'Active'" [class.active]="status() === 'Active'" (click)="setStatus('Active')">
              Đang hoạt động
            </button>
            <button type="button" role="tab" [attr.aria-selected]="status() === 'Removed'" [class.active]="status() === 'Removed'" (click)="setStatus('Removed')">
              Đã ngừng
            </button>
          </div>
          <input class="search" type="search" aria-label="Tìm thành viên" placeholder="Tên hoặc email" [formControl]="searchControl" />
        </div>

        @if (kind(); as value) {
          <app-state [kind]="value" />
        }
        @if (page(); as data) {
          <kendo-grid
            [data]="data.items"
            [pageSize]="pageSize"
            [skip]="(pageNumber() - 1) * pageSize"
            [pageable]="data.total > pageSize ? { info: true, previousNext: true } : false"
            [loading]="reloading()"
            (pageChange)="onPageChange($event)"
          >
            <kendo-grid-column title="Thành viên">
              <ng-template kendoGridCellTemplate let-row>
                <div class="person">
                  <span class="avatar" aria-hidden="true">{{ initials(row.fullName) }}</span>
                  <span class="who">
                    <b>{{ row.fullName }}</b>
                    <span class="cf-muted">{{ row.email }}</span>
                  </span>
                </div>
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Vai trò" [width]="150">
              <ng-template kendoGridCellTemplate let-row>
                <span class="role" [class]="'role role--' + row.role">{{ roleLabel(row.role) }}</span>
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Tài khoản" [width]="200">
              <ng-template kendoGridCellTemplate let-row>
                @if (row.status === 'Removed') {
                  <span class="cf-tag">Đã ngừng</span>
                } @else if (row.mustChangePassword) {
                  <span class="cf-tag cf-tag--warning">Chưa đổi mật khẩu tạm</span>
                } @else {
                  <span class="cf-tag cf-tag--success">Hoạt động</span>
                }
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Tham gia" [width]="120">
              <ng-template kendoGridCellTemplate let-row>{{ row.joinedAt | vnDate }}</ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Thao tác" [width]="320">
              <ng-template kendoGridCellTemplate let-row>
                <div class="row-actions">
                  @if (row.status === 'Removed') {
                    <button kendoButton size="small" fillMode="outline" type="button" (click)="reactivate(row)">Thêm lại</button>
                  } @else if (canAct(row)) {
                    <button kendoButton size="small" fillMode="outline" type="button" [disabled]="!row.canManageAccount" [attr.title]="row.canManageAccount ? null : notManagedHint" (click)="startEdit(row)">Sửa tên</button>
                    <button kendoButton size="small" fillMode="outline" type="button" [disabled]="!row.canManageAccount" [attr.title]="row.canManageAccount ? null : notManagedHint" (click)="resetPassword(row)">Cấp mật khẩu tạm</button>
                    <button kendoButton size="small" fillMode="outline" type="button" (click)="startRemove(row)">Ngừng</button>
                  }
                </div>
              </ng-template>
            </kendo-grid-column>
            <ng-template kendoGridNoRecordsTemplate>
              <p class="no-records">{{ status() === 'Removed' ? 'Chưa có thành viên nào bị ngừng.' : 'Không có thành viên phù hợp.' }}</p>
            </ng-template>
          </kendo-grid>
        }
      </section>
    </div>

    <!-- Mật khẩu tạm: hiện MỘT lần (design-system.md mục 5) -->
    @if (credentials(); as dialog) {
      <kendo-dialog [title]="dialog.title" [width]="480" (close)="closeCredentials()">
        <div class="dialog-body">
          <p class="cf-alert cf-alert--warning" role="status">
            Mật khẩu này sẽ không hiển thị lại. Hãy gửi cho học sinh; họ phải đổi ở lần đăng nhập đầu.
          </p>
          @for (row of dialog.rows; track row.email) {
            <dl class="cred">
              <dt>Email</dt>
              <dd>{{ row.email }}</dd>
              <dt>Mật khẩu tạm</dt>
              <dd><code class="secret">{{ row.password }}</code></dd>
            </dl>
          }
        </div>
        <kendo-dialog-actions>
          <button kendoButton type="button" fillMode="outline" (click)="copy(dialog.rows)">{{ copied() ? 'Đã sao chép' : 'Sao chép' }}</button>
          <button kendoButton type="button" themeColor="primary" (click)="closeCredentials()">Đã lưu, đóng</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }

    <!-- Thêm nhiều email (OQ-20) -->
    @if (bulk(); as dialog) {
      <kendo-dialog title="Thêm nhiều email" [width]="640" (close)="closeBulk()">
        <div class="dialog-body">
          @if (dialog.results === null) {
            <label class="bulk-label" for="bulk-emails">Dán danh sách email (mỗi dòng một email, tối đa {{ maxBulk }})</label>
            <textarea id="bulk-emails" rows="8" class="bulk-input" [formControl]="bulkControl" placeholder="hs1@truong.edu.vn&#10;hs2@truong.edu.vn"></textarea>
            <p class="cf-muted bulk-count" aria-live="polite">
              {{ bulkEmails().length }} email@if (bulkInvalid() > 0) {
                · <span class="invalid">{{ bulkInvalid() }} không hợp lệ</span>
              }
            </p>
            <p class="cf-muted">Email chưa có tài khoản sẽ được tạo kèm mật khẩu tạm; tên tạm lấy từ phần trước @, thủ quỹ sửa lại sau.</p>
            @if (bulkError(); as message) {
              <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
            }
          } @else {
            <p class="cf-alert cf-alert--warning" role="status">Mật khẩu tạm chỉ hiện lúc này. Hãy sao chép trước khi đóng.</p>
            <table class="results">
              <thead>
                <tr><th scope="col">Email</th><th scope="col">Kết quả</th><th scope="col">Mật khẩu tạm</th></tr>
              </thead>
              <tbody>
                @for (row of dialog.results; track row.email) {
                  <tr>
                    <td>{{ row.email }}</td>
                    <td>{{ outcomeLabel(row) }}</td>
                    <td>@if (row.temporaryPassword) { <code class="secret">{{ row.temporaryPassword }}</code> }</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
        <kendo-dialog-actions>
          @if (dialog.results === null) {
            <button kendoButton type="button" fillMode="outline" (click)="closeBulk()">Hủy bỏ</button>
            <button kendoButton type="button" themeColor="primary" [disabled]="bulkBusy() || bulkEmails().length === 0" (click)="submitBulk()">
              {{ bulkBusy() ? 'Đang thêm…' : 'Thêm ' + bulkEmails().length + ' email' }}
            </button>
          } @else {
            @if (bulkCredentials().length > 0) {
              <button kendoButton type="button" fillMode="outline" (click)="copy(bulkCredentials())">{{ copied() ? 'Đã sao chép' : 'Sao chép tất cả mật khẩu' }}</button>
            }
            <button kendoButton type="button" themeColor="primary" (click)="closeBulk()">Đóng</button>
          }
        </kendo-dialog-actions>
      </kendo-dialog>
    }

    <!-- Sửa họ tên -->
    @if (editing(); as member) {
      <kendo-dialog title="Sửa họ tên" [width]="480" (close)="cancelEdit()">
        <form [formGroup]="editForm" (ngSubmit)="saveEdit()" novalidate id="edit-form" class="dialog-body">
          @if (editError(); as message) {
            <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
          }
          <app-auth-field fieldId="edit-name" label="Họ và tên" [maxLength]="100" [control]="editForm.controls.fullName" />
          <p class="cf-muted">{{ member.email }}</p>
        </form>
        <kendo-dialog-actions>
          <button kendoButton type="button" fillMode="outline" (click)="cancelEdit()">Hủy bỏ</button>
          <button kendoButton type="submit" form="edit-form" themeColor="primary" [disabled]="editBusy()">Lưu</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }

    <!-- Ngừng thành viên: liệt kê hệ quả trước khi xác nhận (OQ-15, design-system.md mục 5) -->
    @if (removing(); as dialog) {
      <kendo-dialog title="Ngừng thành viên" [width]="480" (close)="cancelRemove()">
        <div class="dialog-body">
          <p>
            Ngừng <b>{{ dialog.member.fullName }}</b> khỏi quỹ? Họ mất quyền truy cập quỹ; dữ liệu lịch sử vẫn được giữ lại và có thể thêm lại sau.
          </p>
          @if (dialog.preview.blockedReason; as reason) {
            <p class="cf-alert cf-alert--danger" role="alert">{{ reason }}</p>
          }
          @if (dialog.preview.unpaidCount > 0) {
            <p class="cf-alert cf-alert--warning" role="status">
              {{ dialog.preview.unpaidCount }} khoản chưa đóng của khoản thu đang mở sẽ bị hủy.
            </p>
          }
          @if (removeError(); as message) {
            <p class="cf-alert cf-alert--danger" role="alert">{{ message }}</p>
          }
        </div>
        <kendo-dialog-actions>
          <button kendoButton type="button" fillMode="outline" (click)="cancelRemove()">Hủy bỏ</button>
          <button kendoButton type="button" themeColor="error" [disabled]="!dialog.preview.canRemove || removeBusy()" (click)="confirmRemove()">
            {{ removeBusy() ? 'Đang ngừng…' : 'Ngừng thành viên' }}
          </button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }
  `,
  styles: `
    :host {
      --kendo-color-primary: var(--cf-ink);
      --kendo-color-primary-hover: var(--cf-ink-hover);
      --kendo-color-primary-active: var(--cf-ink-hover);
      --kendo-color-primary-emphasis: var(--cf-ink);
      --kendo-border-radius-md: var(--cf-radius-lg);
    }
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip: rect(0 0 0 0);
      white-space: nowrap;
    }
    .add,
    .archived {
      margin-bottom: 16px;
    }
    .add-form {
      display: grid;
      grid-template-columns: 1fr 1fr auto;
      gap: 16px;
      align-items: start;
    }
    .add-actions {
      display: flex;
      gap: 8px;
      margin-top: 26px;
    }
    .toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      margin-bottom: 12px;
    }
    .tabs {
      display: inline-flex;
      gap: 4px;
      padding: 4px;
      border-radius: var(--cf-radius-lg);
      background: var(--cf-subtle);
    }
    .tabs button {
      min-height: 32px;
      padding: 0 12px;
      border: 0;
      border-radius: var(--cf-radius-md);
      background: transparent;
      color: var(--cf-text-muted);
      font: inherit;
      font-weight: 600;
      cursor: pointer;
    }
    .tabs button.active {
      background: var(--cf-surface);
      color: var(--cf-text);
      box-shadow: 0 1px 2px rgba(15, 23, 42, 0.1);
    }
    .search {
      box-sizing: border-box;
      width: 260px;
      min-height: 40px;
      padding: 0 12px;
      border: 1px solid var(--cf-border-strong);
      border-radius: var(--cf-radius-lg);
      font: inherit;
    }
    .person {
      display: flex;
      align-items: center;
      gap: 10px;
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
    .role--Student {
      background: var(--cf-subtle);
      color: var(--cf-text);
    }
    .row-actions {
      display: flex;
      flex-wrap: wrap;
      justify-content: flex-end;
      gap: 6px;
    }
    .no-records {
      margin: 24px 0;
      color: var(--cf-text-muted);
      text-align: center;
    }
    .dialog-body {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .cred {
      display: grid;
      grid-template-columns: 120px 1fr;
      gap: 6px 12px;
      margin: 0;
    }
    .cred dt {
      color: var(--cf-text-muted);
    }
    .cred dd {
      margin: 0;
    }
    .secret {
      padding: 2px 8px;
      border-radius: var(--cf-radius-sm);
      background: var(--cf-subtle);
      font-size: 15px;
      letter-spacing: 0.02em;
      user-select: all;
    }
    .bulk-input {
      box-sizing: border-box;
      width: 100%;
      padding: 8px 12px;
      border: 1px solid var(--cf-border-strong);
      border-radius: var(--cf-radius-lg);
      font: inherit;
    }
    .bulk-count {
      margin: 0;
    }
    .invalid {
      color: var(--cf-danger-fg);
    }
    .results {
      width: 100%;
      border-collapse: collapse;
    }
    .results th,
    .results td {
      padding: 6px 8px;
      border-bottom: 1px solid var(--cf-border);
      text-align: left;
    }
  `,
})
export class MembersPage {
  private readonly api = inject(MembersApi);
  private readonly auth = inject(AuthService);
  private readonly confirm = inject(ConfirmDialog);
  private readonly notifications = inject(AppNotifications);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly fundId = input.required<string>();

  protected readonly pageSize = PAGE_SIZE;
  protected readonly maxBulk = MAX_BULK_EMAILS;
  protected readonly notManagedHint = NOT_MANAGED_HINT;

  protected readonly status = signal<MemberStatus>('Active');
  protected readonly pageNumber = signal(1);
  protected readonly refresh = signal(0);
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  private readonly search = toSignal(
    this.searchControl.valueChanges.pipe(debounceTime(300), map((value) => value.trim()), distinctUntilChanged()),
    { initialValue: '' },
  );

  private readonly query = computed(() => ({
    fundId: this.fundId(),
    status: this.status(),
    search: this.search(),
    page: this.pageNumber(),
    refresh: this.refresh(),
  }));

  private readonly state = toSignal(
    toObservable(this.query).pipe(
      switchMap((query) =>
        toLoadState(
          this.api.list(query.fundId, { status: query.status, search: query.search, page: query.page, pageSize: PAGE_SIZE }),
        ),
      ),
    ),
    { initialValue: LOADING as LoadState<PagedResult<Member>> },
  );

  /** Giữ dữ liệu cũ trên bảng khi đang tải lại để bảng không nhấp nháy. */
  private lastPage: PagedResult<Member> | null = null;
  protected readonly page = computed<PagedResult<Member> | null>(() => {
    const state = this.state();
    if (state.status === 'ready') {
      this.lastPage = state.data;
    }
    return state.status === 'error' ? null : this.lastPage;
  });
  protected readonly reloading = computed(() => this.state().status === 'loading' && this.lastPage !== null);
  /** Trạng thái thay cho bảng: đang tải lần đầu, hoặc lỗi. Có dữ liệu (kể cả rỗng) thì để bảng tự hiện. */
  protected readonly kind = computed(() => {
    const state = this.state();
    if (state.status === 'error') {
      return stateKind(state);
    }
    return state.status === 'loading' && this.lastPage === null ? 'loading' : null;
  });

  private readonly activeTotal = signal(0);
  protected readonly activeCount = this.activeTotal.asReadonly();
  protected readonly fundArchived = signal(false);

  private readonly myRole = computed(() => this.auth.membership(this.fundId())?.role);

  constructor() {
    // Số thành viên đang hoạt động (phụ đề): lấy từ tổng của danh sách khi đang xem tab Đang hoạt động, không lọc tìm kiếm.
    toObservable(this.state).subscribe((state) => {
      if (state.status === 'ready' && this.status() === 'Active' && this.search() === '') {
        this.activeTotal.set(state.data.total);
      }
    });
  }

  // ---------- Danh sách ----------

  protected setStatus(status: MemberStatus): void {
    this.status.set(status);
    this.pageNumber.set(1);
  }

  protected onPageChange(event: PageChangeEvent): void {
    this.pageNumber.set(Math.floor(event.skip / PAGE_SIZE) + 1);
  }

  protected reload(): void {
    this.refresh.update((value) => value + 1);
  }

  protected initials(fullName: string): string {
    return fullName
      .trim()
      .split(/\s+/)
      .filter(Boolean)
      .slice(-2)
      .map((word) => word.charAt(0))
      .join('')
      .toUpperCase();
  }

  protected roleLabel(role: Member['role']): string {
    return ROLE_DISPLAY[role].badge;
  }

  /** AZ-02: thủ quỹ chỉ thao tác trên học sinh; thủ quỹ chính còn thao tác được trên thủ quỹ; không ai thao tác trên thủ quỹ chính. */
  protected canAct(member: Member): boolean {
    return member.role === 'Student' || (member.role === 'Treasurer' && this.myRole() === 'Owner');
  }

  // ---------- Thêm một email ----------

  protected readonly addForm = this.fb.group({
    email: ['', [Validators.required, emailFormatValidator]],
    fullName: ['', [Validators.required, Validators.maxLength(100), (c: { value: string }) => (c.value.trim() ? null : { required: true })]],
  });
  protected readonly adding = signal(false);
  protected readonly addError = signal<string | null>(null);

  protected async addOne(): Promise<void> {
    this.addForm.markAllAsTouched();
    if (this.addForm.invalid || this.adding()) {
      return;
    }

    this.adding.set(true);
    this.addError.set(null);
    try {
      const { email, fullName } = this.addForm.getRawValue();
      const result = await firstValueFrom(this.api.add(this.fundId(), email.trim(), fullName.trim()));
      this.addForm.reset();
      this.reload();
      this.afterAdd(result);
    } catch (error) {
      this.addError.set(applyServerErrors(this.addForm, error, MEMBER_FIELD_FOR));
      if (error instanceof ApiError && error.code === 'fund.archived') {
        this.fundArchived.set(true);
      }
    } finally {
      this.adding.set(false);
    }
  }

  private afterAdd(result: AddMemberResult): void {
    if (result.outcome === 'Created' && result.temporaryPassword) {
      this.openCredentials(`Mật khẩu tạm của ${result.member?.fullName ?? result.email}`, [
        { email: result.email, password: result.temporaryPassword },
      ]);
    } else {
      this.notifications.success(`${OUTCOME_LABELS[result.outcome]}: ${result.member?.fullName ?? result.email}.`);
    }
  }

  protected async reactivate(member: Member): Promise<void> {
    try {
      const result = await firstValueFrom(this.api.add(this.fundId(), member.email, member.fullName));
      this.reload();
      this.notifications.success(`${OUTCOME_LABELS[result.outcome]}: ${member.fullName}.`);
    } catch (error) {
      this.notifyError(error);
    }
  }

  // ---------- Thêm nhiều email ----------

  protected readonly bulkControl = new FormControl('', { nonNullable: true });
  private readonly bulkText = toSignal(this.bulkControl.valueChanges, { initialValue: '' });
  protected readonly bulkEmails = computed(() => uniqueEmails(parseEmails(this.bulkText())));
  protected readonly bulkInvalid = computed(() => countInvalidEmails(this.bulkEmails()));
  protected readonly bulk = signal<{ results: AddMemberResult[] | null } | null>(null);
  protected readonly bulkBusy = signal(false);
  protected readonly bulkError = signal<string | null>(null);
  protected readonly bulkCredentials = computed<CredentialRow[]>(() =>
    (this.bulk()?.results ?? [])
      .filter((row) => row.temporaryPassword)
      .map((row) => ({ email: row.email, password: row.temporaryPassword as string })),
  );

  protected openBulk(): void {
    this.bulkControl.reset('');
    this.bulkError.set(null);
    this.bulk.set({ results: null });
  }

  protected closeBulk(): void {
    this.bulk.set(null);
    this.copied.set(false);
  }

  protected async submitBulk(): Promise<void> {
    const emails = this.bulkEmails();
    if (emails.length === 0 || this.bulkBusy()) {
      return;
    }
    if (emails.length > MAX_BULK_EMAILS) {
      this.bulkError.set(`Mỗi lần chỉ thêm tối đa ${MAX_BULK_EMAILS} email. Bạn đang có ${emails.length}.`);
      return;
    }

    this.bulkBusy.set(true);
    this.bulkError.set(null);
    try {
      const response = await firstValueFrom(this.api.addMany(this.fundId(), emails));
      this.bulk.set({ results: response.results });
      this.reload();
    } catch (error) {
      this.bulkError.set(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
    } finally {
      this.bulkBusy.set(false);
    }
  }

  protected outcomeLabel(row: AddMemberResult): string {
    return row.outcome === 'Invalid' || row.outcome === 'AlreadyExists' ? (row.message ?? OUTCOME_LABELS[row.outcome]) : OUTCOME_LABELS[row.outcome];
  }

  // ---------- Mật khẩu tạm (một lần) ----------

  protected readonly credentials = signal<{ title: string; rows: CredentialRow[] } | null>(null);
  protected readonly copied = signal(false);

  private openCredentials(title: string, rows: CredentialRow[]): void {
    this.copied.set(false);
    this.credentials.set({ title, rows });
  }

  protected closeCredentials(): void {
    this.credentials.set(null);
    this.copied.set(false);
  }

  protected async copy(rows: readonly CredentialRow[]): Promise<void> {
    try {
      await navigator.clipboard.writeText(credentialsText(rows));
      this.copied.set(true);
    } catch {
      this.notifications.warning('Không sao chép được. Hãy bôi đen và chép mật khẩu bằng tay.');
    }
  }

  protected resetPassword(member: Member): void {
    this.confirm
      .confirm({
        title: 'Cấp lại mật khẩu tạm',
        message: `Cấp mật khẩu tạm mới cho ${member.fullName}? Mật khẩu cũ không còn dùng được và các thiết bị đang đăng nhập của họ bị đăng xuất.`,
        confirmLabel: 'Cấp mật khẩu tạm',
      })
      .subscribe(async (confirmed) => {
        if (!confirmed) {
          return;
        }
        try {
          const result = await firstValueFrom(this.api.resetPassword(this.fundId(), member.id));
          this.reload();
          this.openCredentials(`Mật khẩu tạm của ${member.fullName}`, [{ email: member.email, password: result.temporaryPassword }]);
        } catch (error) {
          this.notifyError(error);
        }
      });
  }

  // ---------- Sửa họ tên ----------

  protected readonly editing = signal<Member | null>(null);
  protected readonly editForm = this.fb.group({
    fullName: ['', [Validators.required, Validators.maxLength(100), (c: { value: string }) => (c.value.trim() ? null : { required: true })]],
  });
  protected readonly editBusy = signal(false);
  protected readonly editError = signal<string | null>(null);

  protected startEdit(member: Member): void {
    this.editForm.reset({ fullName: member.fullName });
    this.editError.set(null);
    this.editing.set(member);
  }

  protected cancelEdit(): void {
    this.editing.set(null);
  }

  protected async saveEdit(): Promise<void> {
    const member = this.editing();
    this.editForm.markAllAsTouched();
    if (!member || this.editForm.invalid || this.editBusy()) {
      return;
    }

    this.editBusy.set(true);
    this.editError.set(null);
    try {
      await firstValueFrom(this.api.update(this.fundId(), member.id, this.editForm.controls.fullName.value.trim()));
      this.editing.set(null);
      this.reload();
      this.notifications.success('Đã lưu họ tên.');
    } catch (error) {
      this.editError.set(applyServerErrors(this.editForm, error, MEMBER_FIELD_FOR));
    } finally {
      this.editBusy.set(false);
    }
  }

  // ---------- Ngừng thành viên ----------

  protected readonly removing = signal<{ member: Member; preview: RemovalPreview } | null>(null);
  protected readonly removeBusy = signal(false);
  protected readonly removeError = signal<string | null>(null);

  protected async startRemove(member: Member): Promise<void> {
    try {
      const preview = await firstValueFrom(this.api.removalPreview(this.fundId(), member.id));
      this.removeError.set(null);
      this.removing.set({ member, preview });
    } catch (error) {
      this.notifyError(error);
    }
  }

  protected cancelRemove(): void {
    this.removing.set(null);
  }

  protected async confirmRemove(): Promise<void> {
    const dialog = this.removing();
    if (!dialog || this.removeBusy()) {
      return;
    }

    this.removeBusy.set(true);
    this.removeError.set(null);
    try {
      await firstValueFrom(this.api.remove(this.fundId(), dialog.member.id));
      this.removing.set(null);
      this.reload();
      this.notifications.success(`Đã ngừng ${dialog.member.fullName}.`);
    } catch (error) {
      this.removeError.set(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
    } finally {
      this.removeBusy.set(false);
    }
  }

  private notifyError(error: unknown): void {
    this.notifications.error(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
  }
}
