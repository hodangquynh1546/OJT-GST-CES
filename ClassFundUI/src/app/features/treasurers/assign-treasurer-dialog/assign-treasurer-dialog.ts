import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { LoaderComponent } from '@progress/kendo-angular-indicators';
import { RadioButtonDirective } from '@progress/kendo-angular-inputs';
import { finalize } from 'rxjs';

import { ApiError } from '../../../shared/models/api-error';
import { AppState } from '../../../shared/components/app-state/app-state';
import { LoadState } from '../../../shared/state/load-state';
import { TreasurersApi } from '../treasurers.api';
import { StudentOption } from '../treasurers.models';

type Tone = 'success' | 'warning' | 'danger' | 'neutral' | 'primary';

interface Candidate {
  student: StudentOption;
  eligible: boolean;
  sub: string;
  tag: string;
  tone: Tone;
}

export interface AssignedTreasurer {
  id: string;
  studentName: string;
}

/**
 * Dialog "Phân công thủ quỹ" 520 px (Canvas board `Treasurers`; FR-27, BR-31): danh sách radio học sinh của quỹ,
 * chỉ học sinh có tài khoản đã kích hoạt, không bị khóa và chưa là thủ quỹ mới chọn được; lý do hiện cạnh tên.
 */
@Component({
  selector: 'app-assign-treasurer-dialog',
  imports: [RouterLink, ButtonComponent, DialogComponent, DialogActionsComponent, LoaderComponent, RadioButtonDirective, AppState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <kendo-dialog title="Phân công thủ quỹ" [width]="520" (close)="cancel()">
      <div class="body">
        <span class="cf-muted hint">
          Chỉ học sinh của quỹ này, có tài khoản <b>đã kích hoạt</b> và không bị khóa.
        </span>

        @switch (students().status) {
          @case ('loading') {
            <app-state kind="loading" />
          }
          @case ('error') {
            <app-state kind="error" actionLabel="Thử lại" (action)="retry.emit()" />
          }
          @default {
            @if (candidates().length === 0) {
              <span class="cf-muted">
                Quỹ chưa có học sinh. Thêm học sinh ở trang
                <a [routerLink]="['/funds', fundId(), 'students']" (click)="cancel()">Học sinh &amp; phụ huynh</a> trước.
              </span>
            } @else {
              <div class="options" role="radiogroup" aria-label="Chọn học sinh">
                @for (candidate of candidates(); track candidate.student.id) {
                  <label
                    class="opt"
                    [class.opt--on]="picked() === candidate.student.id"
                    [class.opt--disabled]="!candidate.eligible"
                  >
                    <input
                      type="radio"
                      kendoRadioButton
                      name="treasurer-student"
                      [value]="candidate.student.id"
                      [checked]="picked() === candidate.student.id"
                      [disabled]="!candidate.eligible || saving()"
                      [attr.aria-describedby]="'opt-' + candidate.student.id"
                      (change)="pick(candidate.student.id)"
                    />
                    <span class="who">
                      <b>{{ candidate.student.fullName }}</b>
                      <span class="cf-muted sub" [id]="'opt-' + candidate.student.id">{{ candidate.sub }}</span>
                    </span>
                    <span class="cf-tag" [class]="'tone-' + candidate.tone">{{ candidate.tag }}</span>
                  </label>
                }
              </div>
            }
          }
        }

        @if (pickError()) {
          <span class="pick-error" role="alert">Chọn một học sinh đủ điều kiện.</span>
        }
        @if (serverError(); as message) {
          <div class="cf-alert cf-alert--danger" role="alert">{{ message }}</div>
        }
      </div>

      <kendo-dialog-actions layout="end">
        <button kendoButton type="button" [disabled]="saving()" (click)="cancel()">Hủy bỏ</button>
        <button kendoButton themeColor="primary" type="button" [disabled]="saving()" [attr.aria-busy]="saving()" (click)="assign()">
          @if (saving()) {
            <kendo-loader size="small" type="infinite-spinner" themeColor="light" />
            Đang phân công…
          } @else {
            Phân công
          }
        </button>
      </kendo-dialog-actions>
    </kendo-dialog>
  `,
  styles: `
    .body {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .hint {
      font-size: 13px;
    }
    .options {
      display: flex;
      flex-direction: column;
      gap: 8px;
      max-height: 360px;
      overflow: auto;
    }
    .opt {
      display: flex;
      align-items: center;
      gap: 10px;
      box-sizing: border-box;
      min-height: 52px;
      padding: 8px 12px;
      border: 1px solid var(--cf-border);
      border-radius: var(--cf-radius-lg);
      background: var(--cf-surface);
      cursor: pointer;
    }
    .opt--on {
      border-color: var(--cf-primary);
      background: var(--cf-primary-tint);
    }
    .opt--disabled {
      cursor: not-allowed;
      opacity: 0.6;
    }
    .opt:has(input:focus-visible) {
      outline: 2px solid var(--cf-primary);
      outline-offset: 2px;
    }
    .who {
      display: flex;
      flex: 1;
      flex-direction: column;
      min-width: 0;
    }
    .sub {
      font-size: 12px;
    }
    .tone-success {
      background: var(--cf-success-bg);
      color: var(--cf-success-fg);
    }
    .tone-warning {
      background: var(--cf-warning-bg);
      color: var(--cf-warning-fg);
    }
    .tone-danger {
      background: var(--cf-danger-bg);
      color: var(--cf-danger-fg);
    }
    .tone-primary {
      background: var(--cf-primary-tint);
      color: var(--cf-primary-dark);
    }
    .pick-error {
      color: var(--cf-danger-fg);
      font-size: 13px;
    }
    kendo-loader {
      margin-right: 6px;
    }
  `,
})
export class AssignTreasurerDialog {
  private readonly api = inject(TreasurersApi);
  private readonly destroyRef = inject(DestroyRef);

  readonly fundId = input.required<string>();
  /** Học sinh của quỹ (`GET /funds/{id}/students`), trang G-03 tải khi mở dialog. */
  readonly students = input.required<LoadState<StudentOption[]>>();
  /** Học sinh đang là thủ quỹ theo `GET /treasurers` (nguồn chính, kể cả khi danh sách học sinh chưa cập nhật). */
  readonly activeStudentIds = input<readonly string[]>([]);

  readonly assigned = output<AssignedTreasurer>();
  readonly closed = output<void>();
  readonly retry = output<void>();

  protected readonly picked = signal<string | null>(null);
  protected readonly pickError = signal(false);
  protected readonly serverError = signal<string | null>(null);
  protected readonly saving = signal(false);

  protected readonly candidates = computed<Candidate[]>(() => {
    const state = this.students();
    if (state.status !== 'ready') {
      return [];
    }
    const active = new Set(this.activeStudentIds());
    return state.data.map((student) => toCandidate(student, student.isTreasurer || active.has(student.id)));
  });

  protected pick(studentId: string): void {
    this.picked.set(studentId);
    this.pickError.set(false);
    this.serverError.set(null);
  }

  protected assign(): void {
    if (this.saving()) {
      return;
    }
    const candidate = this.candidates().find((item) => item.student.id === this.picked() && item.eligible);
    if (!candidate) {
      this.pickError.set(true);
      return;
    }

    this.serverError.set(null);
    this.saving.set(true);
    this.api
      .assign(this.fundId(), candidate.student.id)
      .pipe(
        finalize(() => this.saving.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (id) => this.assigned.emit({ id, studentName: candidate.student.fullName }),
        error: (error: unknown) =>
          this.serverError.set(error instanceof ApiError ? error.message : 'Đã có lỗi xảy ra. Vui lòng thử lại.'),
      });
  }

  protected cancel(): void {
    if (!this.saving()) {
      this.closed.emit();
    }
  }
}

function toCandidate(student: StudentOption, isTreasurer: boolean): Candidate {
  if (isTreasurer) {
    return { student, eligible: false, sub: student.username ?? '', tag: 'Đang là thủ quỹ', tone: 'primary' };
  }
  switch (student.accountStatus) {
    case 'Active':
      return { student, eligible: true, sub: student.username ?? '', tag: 'Đã kích hoạt', tone: 'success' };
    case 'PendingActivation':
      return { student, eligible: false, sub: `${student.username} · chưa đổi mật khẩu tạm`, tag: 'Chờ kích hoạt', tone: 'warning' };
    case 'Disabled':
      return { student, eligible: false, sub: student.username ?? '', tag: 'Bị khóa', tone: 'danger' };
    default:
      return { student, eligible: false, sub: 'Cấp tài khoản ở trang Học sinh trước', tag: 'Chưa có tài khoản', tone: 'neutral' };
  }
}
