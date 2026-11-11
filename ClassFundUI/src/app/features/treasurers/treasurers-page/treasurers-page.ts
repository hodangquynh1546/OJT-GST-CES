import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { LoaderComponent } from '@progress/kendo-angular-indicators';
import { filter, finalize } from 'rxjs';

import { AppNotifications } from '../../../core/services/notification.service';
import { ApiError } from '../../../shared/models/api-error';
import { AppState } from '../../../shared/components/app-state/app-state';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { AssignTreasurerDialog, AssignedTreasurer } from '../assign-treasurer-dialog/assign-treasurer-dialog';
import { TreasurersApi } from '../treasurers.api';
import { StudentOption, TreasurerAssignment } from '../treasurers.models';
import { vnDateTime } from '../vn-date-time';

interface ActiveRow {
  assignment: TreasurerAssignment;
  initial: string;
  meta: string;
}

interface HistoryRow {
  assignment: TreasurerAssignment;
  assigned: string;
  revoked: string;
}

/**
 * G-03 "Phân công thủ quỹ" – Canvas board `Treasurers`; FR-27, BR-31, BR-32, BR-33, BR-36, P-15.
 * Đổi người: phân công người mới trước, thu hồi người cũ sau; nút "Thu hồi" của thủ quỹ cuối cùng bị khóa.
 */
@Component({
  selector: 'app-treasurers-page',
  imports: [ButtonComponent, LoaderComponent, AppState, PageHeader, AssignTreasurerDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <app-page-header
        title="Phân công thủ quỹ"
        subtitle="Một quỹ có thể có nhiều thủ quỹ. Thủ quỹ là học sinh của quỹ; ghi thu chi và quản lý danh mục."
      >
        @if (assignments()) {
          <button kendoButton themeColor="primary" type="button" (click)="openAssign()">+ Phân công thủ quỹ</button>
        }
      </app-page-header>

      @if (assignments(); as all) {
        @if (active().length === 0) {
          <div class="cf-alert cf-alert--warning banner" role="status">
            <b>Quỹ chưa phân công thủ quỹ.</b> Chưa ai ghi thu chi được. Chọn một học sinh đã kích hoạt tài khoản để
            phân công.
            <span class="banner-note">
              Quỹ chuyển từ phiên bản cũ cũng ở trạng thái này: dữ liệu cũ giữ nguyên, người phụ trách cũ nay là GVCN.
            </span>
          </div>
        }

        <section class="cf-card card" aria-labelledby="tr-active">
          <h2 id="tr-active">Đang là thủ quỹ ({{ active().length }})</h2>
          @for (row of active(); track row.assignment.id) {
            <div class="row">
              <span class="avatar" aria-hidden="true">{{ row.initial }}</span>
              <div class="who">
                <b>{{ row.assignment.studentName }}</b>
                <span class="cf-muted small">{{ row.meta }}</span>
              </div>
              @if (isLast()) {
                <span class="last">
                  <button kendoButton type="button" [disabled]="true" aria-describedby="tr-last-hint">Thu hồi</button>
                  <span id="tr-last-hint" class="cf-muted small">Thủ quỹ cuối cùng – phân công người mới trước</span>
                </span>
              } @else {
                <button
                  kendoButton
                  class="danger"
                  type="button"
                  [disabled]="revoking() !== null"
                  [attr.aria-label]="'Thu hồi ' + row.assignment.studentName"
                  (click)="askRevoke(row.assignment.id)"
                >
                  Thu hồi
                </button>
              }
            </div>
            @if (confirming() === row.assignment.id) {
              <div class="cf-alert cf-alert--danger confirm" role="alert">
                <span>
                  <b>Thu hồi quyền thủ quỹ của {{ row.assignment.studentName }}?</b> Học sinh trở về quyền xem công khai
                  ngay ở thao tác tiếp theo. Giao dịch đã ghi giữ nguyên người ghi. Lịch sử phân công được lưu.
                </span>
                <div class="actions">
                  <button kendoButton type="button" [disabled]="revoking() !== null" (click)="confirming.set(null)">
                    Không
                  </button>
                  <button
                    kendoButton
                    class="danger"
                    type="button"
                    [disabled]="revoking() !== null"
                    [attr.aria-busy]="revoking() === row.assignment.id"
                    (click)="revoke(row.assignment)"
                  >
                    @if (revoking() === row.assignment.id) {
                      <kendo-loader size="small" type="infinite-spinner" />
                      Đang thu hồi…
                    } @else {
                      Thu hồi
                    }
                  </button>
                </div>
              </div>
            }
          } @empty {
            <span class="cf-muted">Chưa có thủ quỹ.</span>
          }
          @if (revokeError(); as message) {
            <div class="cf-alert cf-alert--danger" role="alert">{{ message }}</div>
          }
          <div class="note" role="note">
            <b>Đổi thủ quỹ:</b> 1) “+ Phân công thủ quỹ” cho người mới → 2) “Thu hồi” người cũ. Phụ huynh của thủ quỹ
            <b>không</b> có quyền thủ quỹ.
          </div>
        </section>

        <section class="cf-card card" aria-labelledby="tr-hist">
          <h2 id="tr-hist">Lịch sử phân công</h2>
          @if (all.length === 0) {
            <span class="cf-muted">Chưa có lịch sử phân công.</span>
          } @else {
            <table class="history" aria-labelledby="tr-hist">
              <thead>
                <tr>
                  <th scope="col">Học sinh</th>
                  <th scope="col">Phân công</th>
                  <th scope="col">Thu hồi</th>
                  <th scope="col">Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                @for (row of history(); track row.assignment.id) {
                  <tr>
                    <td>
                      <b>{{ row.assignment.studentName }}</b>
                    </td>
                    <td>{{ row.assigned }}</td>
                    <td>{{ row.revoked }}</td>
                    <td>
                      @if (row.assignment.isActive) {
                        <span class="cf-tag cf-tag--success">Đang hiệu lực</span>
                      } @else {
                        <span class="cf-tag">Đã thu hồi</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </section>
      } @else {
        @switch (kind()) {
          @case ('error') {
            <app-state class="cf-card" kind="error" actionLabel="Thử lại" (action)="load()" />
          }
          @default {
            <app-state [kind]="kind()!" />
          }
        }
      }
    </section>

    @if (dialogOpen()) {
      <app-assign-treasurer-dialog
        [fundId]="fundId()"
        [students]="students()"
        [activeStudentIds]="activeStudentIds()"
        (assigned)="onAssigned($event)"
        (closed)="dialogOpen.set(false)"
        (retry)="loadStudents()"
      />
    }
  `,
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      gap: 16px;
      /* Canvas: main tối đa 1100 px gồm padding 24 px hai bên. */
      max-width: 1052px;
    }
    app-page-header {
      margin-bottom: 0;
    }
    .banner {
      padding: 12px 14px;
    }
    .banner-note {
      display: block;
      margin-top: 4px;
      font-size: 13px;
    }
    .card {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    h2 {
      margin: 0;
      font-size: 17px;
    }
    .row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 12px;
      padding: 12px 0;
      border-bottom: 1px solid var(--cf-subtle);
    }
    .avatar {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 36px;
      height: 36px;
      border-radius: 50%;
      background: var(--cf-primary-tint);
      color: var(--cf-primary-dark);
      font-weight: 700;
    }
    .who {
      display: flex;
      flex: 1 1 200px;
      flex-direction: column;
      min-width: 0;
    }
    .small {
      font-size: 12px;
    }
    .last {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 2px;
    }
    .danger {
      border-color: var(--cf-expense);
      color: var(--cf-danger-fg);
    }
    .confirm {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .actions {
      display: flex;
      gap: 8px;
    }
    .note {
      padding: 10px 12px;
      border-radius: var(--cf-radius-md);
      background: var(--cf-subtle);
      font-size: 13px;
    }
    .history {
      width: 100%;
      border-collapse: collapse;
      table-layout: fixed;
    }
    .history th,
    .history td {
      padding: 8px 12px 8px 0;
      border-bottom: 1px solid var(--cf-subtle);
      font-size: 13px;
      text-align: left;
      vertical-align: middle;
    }
    .history th {
      color: var(--cf-text-muted);
      font-size: 12px;
      font-weight: 700;
    }
    /* Canvas: cột 1.4fr · 1.2fr · 1.2fr · 110 px. */
    .history th:nth-child(1) {
      width: calc((100% - 110px) * 1.4 / 3.8);
    }
    .history th:nth-child(2),
    .history th:nth-child(3) {
      width: calc((100% - 110px) * 1.2 / 3.8);
    }
    .history th:nth-child(4) {
      width: 110px;
    }
    kendo-loader {
      margin-right: 6px;
    }
  `,
})
export class TreasurersPage {
  private readonly api = inject(TreasurersApi);
  private readonly notifications = inject(AppNotifications);
  private readonly destroyRef = inject(DestroyRef);

  /** `:fundId` trên URL (component input binding, kế thừa tham số route cha). */
  readonly fundId = input.required<string>();

  protected readonly state = signal<LoadState<TreasurerAssignment[]>>(LOADING);
  protected readonly students = signal<LoadState<StudentOption[]>>(LOADING);
  protected readonly dialogOpen = signal(false);
  protected readonly confirming = signal<string | null>(null);
  protected readonly revoking = signal<string | null>(null);
  protected readonly revokeError = signal<string | null>(null);

  protected readonly kind = computed(() => stateKind(this.state(), () => false));
  protected readonly assignments = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data : null;
  });
  private readonly usernames = computed(() => {
    const students = this.students();
    return new Map(students.status === 'ready' ? students.data.map((student) => [student.id, student.username]) : []);
  });
  protected readonly active = computed<ActiveRow[]>(() =>
    (this.assignments() ?? [])
      .filter((assignment) => assignment.isActive)
      .map((assignment) => {
        const username = this.usernames().get(assignment.studentId);
        const assigned = `Phân công ${vnDateTime(assignment.assignedAt)} bởi ${assignment.assignedByName}`;
        return { assignment, initial: initialOf(assignment.studentName), meta: username ? `${assigned} · ${username}` : assigned };
      }),
  );
  protected readonly isLast = computed(() => this.active().length === 1);
  protected readonly activeStudentIds = computed(() => this.active().map((row) => row.assignment.studentId));
  protected readonly history = computed<HistoryRow[]>(() =>
    (this.assignments() ?? []).map((assignment) => ({
      assignment,
      assigned: `${vnDateTime(assignment.assignedAt)} · ${assignment.assignedByName}`,
      revoked: assignment.revokedAt ? `${vnDateTime(assignment.revokedAt)} · ${assignment.revokedByName ?? ''}` : '—',
    })),
  );

  constructor() {
    effect(() => {
      this.fundId();
      untracked(() => {
        this.load();
        this.loadStudents();
      });
    });
  }

  /** `silent` giữ dữ liệu cũ trên màn hình trong lúc tải lại (sau phân công / thu hồi). */
  protected load(silent = false): void {
    toLoadState(this.api.list(this.fundId()))
      .pipe(
        filter((state) => !silent || state.status !== 'loading'),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((state) => this.state.set(state));
  }

  /** Học sinh của quỹ: tên đăng nhập cạnh thủ quỹ và danh sách chọn trong dialog. Lỗi chỉ ảnh hưởng dialog. */
  protected loadStudents(): void {
    toLoadState(this.api.students(this.fundId()))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((state) => this.students.set(state));
  }

  protected openAssign(): void {
    this.confirming.set(null);
    this.revokeError.set(null);
    this.loadStudents();
    this.dialogOpen.set(true);
  }

  protected onAssigned(assigned: AssignedTreasurer): void {
    this.dialogOpen.set(false);
    this.notifications.success(`Đã phân công ${assigned.studentName} làm thủ quỹ.`);
    this.load(true);
  }

  protected askRevoke(assignmentId: string): void {
    this.revokeError.set(null);
    this.confirming.set(assignmentId);
  }

  protected revoke(assignment: TreasurerAssignment): void {
    if (this.revoking()) {
      return;
    }
    this.revoking.set(assignment.id);
    this.api
      .revoke(this.fundId(), assignment.id)
      .pipe(
        finalize(() => this.revoking.set(null)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => {
          this.confirming.set(null);
          this.notifications.success(`Đã thu hồi quyền thủ quỹ của ${assignment.studentName}.`);
          this.load(true);
        },
        error: (error: unknown) => {
          this.confirming.set(null);
          if (error instanceof ApiError && (error.status === 403 || error.status === 404)) {
            this.state.set({ status: 'error', error });
            return;
          }
          // treasurer.last (người khác vừa thu hồi cùng lúc), treasurer.already_revoked: hiện lý do và tải lại danh sách.
          this.revokeError.set(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.');
          this.load(true);
        },
      });
  }
}

/** Chữ cái đầu của tên (từ cuối trong họ tên tiếng Việt). */
function initialOf(fullName: string): string {
  const name = fullName.trim().split(/\s+/).pop() ?? '';
  return name.charAt(0).toUpperCase();
}
