import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { CellClickEvent, ColumnComponent, GridComponent, CellTemplateDirective } from '@progress/kendo-angular-grid';
import { TextBoxDirective } from '@progress/kendo-angular-inputs';

import { CurrentFund } from '../../../core/services/current-fund.service';
import { AppNotifications } from '../../../core/services/notification.service';
import { AppState } from '../../../shared/components/app-state/app-state';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { AddStudentDialog, AddedStudent } from '../add-student-dialog/add-student-dialog';
import { CredentialDialog, CredentialNotice } from '../credential-dialog/credential-dialog';
import { StudentDrawer, StudentMutation } from '../student-drawer/student-drawer';
import { STUDENT_FILTERS, StudentFilter, countByFilter, filterStudents } from '../students-filter';
import { StudentsApi } from '../students.api';
import { ACCOUNT_STATUS, Student } from '../students.models';

/**
 * G-02 "Học sinh & phụ huynh" (GVCN, `Student.Manage`) – Canvas board `Students`; FR-24, FR-25, FR-26.
 * Danh sách là nguồn dữ liệu duy nhất: mọi thao tác (thêm, cấp tài khoản, liên kết…) xong thì tải lại danh sách.
 * Mật khẩu tạm chỉ nằm trong signal `credential` tới khi GVCN đóng hộp thoại.
 */
@Component({
  selector: 'app-students-page',
  imports: [
    ButtonComponent,
    CellTemplateDirective,
    ColumnComponent,
    GridComponent,
    TextBoxDirective,
    AppState,
    PageHeader,
    AddStudentDialog,
    CredentialDialog,
    StudentDrawer,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <app-page-header title="Học sinh & phụ huynh" [subtitle]="summary()">
        <button kendoButton themeColor="primary" type="button" (click)="addOpen.set(true)">+ Thêm học sinh</button>
      </app-page-header>

      <div class="cf-alert note" role="note">
        Chỉ GVCN thấy trang này. Hệ thống <b>không gửi email</b>: mật khẩu tạm của học sinh / phụ huynh chỉ hiện
        <b>một lần</b> – hãy tự chuyển cho người nhận. Học sinh phải đổi mật khẩu tạm (kích hoạt) trước khi được phân
        công thủ quỹ.
      </div>

      @if (students(); as all) {
        <div class="toolbar">
          <input
            kendoTextBox
            class="search"
            type="search"
            aria-label="Tìm học sinh"
            placeholder="Tìm theo họ tên hoặc tên đăng nhập"
            [value]="query()"
            (input)="query.set($any($event.target).value)"
          />
          <div class="chips" role="radiogroup" aria-label="Lọc trạng thái">
            @for (item of filters; track item.key) {
              <button
                type="button"
                role="radio"
                class="chip"
                [attr.aria-checked]="filter() === item.key"
                [class.chip--on]="filter() === item.key"
                (click)="filter.set(item.key)"
              >
                {{ item.label }} ({{ counts()[item.key] }})
              </button>
            }
          </div>
        </div>

        <div class="cf-card table" aria-label="Danh sách học sinh">
          <kendo-grid [data]="rows()" scrollable="none" [navigable]="true" (cellClick)="onCellClick($event)">
            <kendo-grid-column title="Học sinh" [width]="260">
              <ng-template kendoGridCellTemplate let-student>
                <div class="cell-stack">
                  <b class="ellipsis">{{ student.fullName }}</b>
                  <span class="cf-muted small">{{ student.username ?? 'Chưa có tên đăng nhập' }}</span>
                </div>
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Tài khoản" [width]="150">
              <ng-template kendoGridCellTemplate let-student>
                <span class="cf-tag" [class]="tone(student)">{{ label(student) }}</span>
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Vai trò" [width]="120">
              <ng-template kendoGridCellTemplate let-student>
                @if (student.isTreasurer) {
                  <span class="cf-tag treasurer">Thủ quỹ</span>
                } @else {
                  <span class="cf-muted small">Học sinh</span>
                }
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Phụ huynh">
              <ng-template kendoGridCellTemplate let-student>
                <div class="cell-stack small">
                  <span class="ellipsis">{{ student.parent ? student.parent.fullName : '— Chưa liên kết' }}</span>
                  @if (student.parent) {
                    <span class="cf-muted">
                      {{ student.parent.email }}{{ student.parent.pendingActivation ? ' · chờ kích hoạt' : '' }}
                    </span>
                  }
                </div>
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="" [width]="100">
              <ng-template kendoGridCellTemplate let-student>
                <button kendoButton fillMode="link" themeColor="primary" type="button" [attr.aria-label]="'Mở ' + student.fullName" (click)="open(student)">
                  Chi tiết ›
                </button>
              </ng-template>
            </kendo-grid-column>
          </kendo-grid>
          @if (rows().length === 0) {
            <div class="cf-muted empty" role="status">
              {{ all.length === 0 ? 'Chưa có học sinh nào. Bấm “+ Thêm học sinh” để bắt đầu.' : 'Không có học sinh phù hợp.' }}
            </div>
          }
        </div>
      } @else {
        @switch (kind()) {
          @case ('error') {
            <app-state class="cf-card" kind="error" actionLabel="Thử lại" (action)="load()" />
          }
          @case ('loading') {
            <app-state kind="loading" />
          }
          @default {
            <app-state [kind]="kind() === 'forbidden' ? 'forbidden' : 'not-found'" />
          }
        }
      }

      @if (selected(); as student) {
        <app-student-drawer
          [fundId]="fundId()"
          [student]="student"
          [blocked]="addOpen() || credential() !== null"
          (closed)="selectedId.set(null)"
          (mutated)="onMutated($event)"
        />
      }
      @if (addOpen()) {
        <app-add-student-dialog [fundId]="fundId()" (added)="onAdded($event)" (closed)="addOpen.set(false)" />
      }
      @if (credential(); as notice) {
        <app-credential-dialog [notice]="notice" (closed)="credential.set(null)" />
      }
    </section>
  `,
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      gap: 16px;
      max-width: var(--cf-content-max);
    }
    app-page-header {
      margin-bottom: 0;
    }
    .note {
      background: var(--cf-subtle);
      color: var(--cf-text);
      font-size: 13px;
    }
    .toolbar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .search {
      flex: 1 1 240px;
    }
    .chips {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .chip {
      min-height: 32px;
      padding: 0 12px;
      border: 1px solid var(--cf-border-strong);
      border-radius: 16px;
      background: var(--cf-surface);
      color: var(--cf-text);
      font: inherit;
      font-size: 13px;
      cursor: pointer;
    }
    .chip--on {
      border-color: var(--cf-primary);
      background: var(--cf-primary-tint);
      color: var(--cf-primary-dark);
    }
    .table {
      padding: 0;
      overflow: hidden;
    }
    kendo-grid {
      border: 0;
    }
    .cell-stack {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }
    .ellipsis {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .small {
      font-size: 12px;
    }
    .cell-stack.small span:first-child {
      font-size: 13px;
    }
    .treasurer {
      background: var(--cf-primary-tint);
      color: var(--cf-primary-dark);
    }
    .empty {
      padding: 28px;
      text-align: center;
    }
    ::ng-deep .k-grid tbody tr {
      cursor: pointer;
    }
  `,
})
export class StudentsPage {
  private readonly api = inject(StudentsApi);
  private readonly currentFund = inject(CurrentFund);
  private readonly notifications = inject(AppNotifications);
  private readonly destroyRef = inject(DestroyRef);

  /** `:fundId` trên URL (component input binding). */
  readonly fundId = input.required<string>();

  protected readonly filters = STUDENT_FILTERS;
  protected readonly state = signal<LoadState<Student[]>>(LOADING);
  protected readonly query = signal('');
  protected readonly filter = signal<StudentFilter>('all');
  protected readonly selectedId = signal<string | null>(null);
  protected readonly addOpen = signal(false);
  protected readonly credential = signal<CredentialNotice | null>(null);

  protected readonly kind = computed(() => stateKind(this.state(), () => false));
  protected readonly students = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data : null;
  });
  protected readonly counts = computed(() => countByFilter(this.students() ?? []));
  protected readonly rows = computed(() => filterStudents(this.students() ?? [], this.query(), this.filter()));
  protected readonly selected = computed(() => this.students()?.find((student) => student.id === this.selectedId()) ?? null);
  protected readonly summary = computed(() => {
    const all = this.students();
    if (!all) {
      return '';
    }
    const counts = this.counts();
    const fundName = this.currentFund.membership()?.fundName;
    return [
      fundName,
      `${counts.all} học sinh`,
      `${counts.treasurer} thủ quỹ`,
      `${counts.all - counts.noParent} phụ huynh đã liên kết`,
    ]
      .filter(Boolean)
      .join(' · ');
  });

  constructor() {
    effect(() => {
      this.fundId();
      untracked(() => this.load());
    });
  }

  protected load(): void {
    this.state.set(LOADING);
    toLoadState(this.api.list(this.fundId()))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((state) => this.state.set(state));
  }

  protected label = (student: Student): string => ACCOUNT_STATUS[student.accountStatus].label;

  protected tone = (student: Student): string => {
    const tone = ACCOUNT_STATUS[student.accountStatus].tone;
    return tone ? `cf-tag--${tone}` : '';
  };

  protected open(student: Student): void {
    this.selectedId.set(student.id);
  }

  protected onCellClick(event: CellClickEvent): void {
    this.open(event.dataItem as Student);
  }

  protected onAdded(added: AddedStudent): void {
    this.addOpen.set(false);
    const parent = added.created.parent;
    if (parent?.credential) {
      this.credential.set({
        title: 'Tài khoản phụ huynh mới',
        recipient: 'phụ huynh',
        loginLabel: 'Email đăng nhập',
        login: parent.credential.login,
        password: parent.credential.temporaryPassword,
      });
    }
    const linkedExisting = added.hadParentEmail && parent && !parent.credential;
    this.notifications.success(
      `Đã thêm học sinh ${added.name}.${linkedExisting ? ' Đã liên kết với tài khoản phụ huynh hiện có.' : ''}`,
    );
    this.reload();
  }

  protected onMutated(mutation: StudentMutation): void {
    if (mutation.credential) {
      this.credential.set(mutation.credential);
    }
    if (mutation.message) {
      this.notifications.success(mutation.message);
    }
    this.reload();
  }

  /** Tải lại danh sách mà không nháy trạng thái loading (drawer / dialog đang mở). */
  private reload(): void {
    this.api
      .list(this.fundId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: (data) => this.state.set({ status: 'ready', data }) });
  }
}
