import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { AppState } from '../../../shared/components/app-state/app-state';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { MembersApi } from '../members.api';
import { MEMBER_ROLE_LABEL, Member, MemberRole } from '../members.models';

type RoleFilter = 'all' | MemberRole;

const FILTERS: readonly { key: RoleFilter; label: string }[] = [
  { key: 'all', label: 'Tất cả' },
  { key: 'Teacher', label: MEMBER_ROLE_LABEL.Teacher },
  { key: 'Treasurer', label: MEMBER_ROLE_LABEL.Treasurer },
  { key: 'Student', label: MEMBER_ROLE_LABEL.Student },
];

/** Chữ cái đầu của tên (từ cuối trong họ tên tiếng Việt). */
function initialOf(fullName: string): string {
  return (fullName.trim().split(/\s+/).pop() ?? '').charAt(0).toUpperCase();
}

/**
 * T-03 "Thành viên quỹ" – Canvas board `Members`; FR-12, BR-23. Chỉ xem họ tên và vai trò (không email / tên đăng nhập);
 * GVCN và thủ quỹ vào được (guard `Member.View`).
 */
@Component({
  selector: 'app-members-page',
  imports: [AppState, PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="page">
      <app-page-header title="Thành viên quỹ" subtitle="Chỉ xem họ tên và vai trò" />

      @if (members(); as all) {
        <div class="cf-alert note" role="note">
          Danh sách <b>chỉ xem</b>. Thêm học sinh, cấp tài khoản, liên kết phụ huynh và phân công thủ quỹ do
          <b>giáo viên chủ nhiệm</b> thực hiện. Không hiển thị email, tên đăng nhập hay thông tin liên hệ.
        </div>

        <div class="chips" role="radiogroup" aria-label="Lọc vai trò">
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

        <section class="cf-card card" aria-label="Danh sách thành viên">
          @for (member of rows(); track $index) {
            <div class="row">
              <span class="avatar" [attr.data-role]="member.role" aria-hidden="true">{{ initialOf(member.fullName) }}</span>
              <b class="name">{{ member.fullName }}</b>
              <span class="role" [attr.data-role]="member.role">{{ roleLabel[member.role] }}</span>
            </div>
          } @empty {
            <span class="cf-muted">Không có thành viên nào.</span>
          }
        </section>
        <span class="sr-only" aria-live="polite">{{ rows().length }} thành viên đang hiển thị ({{ all.length }} tổng)</span>
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
  `,
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      gap: 16px;
      max-width: 852px;
    }
    app-page-header {
      margin-bottom: 0;
    }
    .note {
      background: var(--cf-primary-tint);
      color: var(--cf-primary-dark);
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
    .card {
      display: flex;
      flex-direction: column;
      gap: 0;
    }
    .row {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 0;
      border-bottom: 1px solid var(--cf-subtle);
    }
    .name {
      flex: 1;
      min-width: 0;
      overflow-wrap: anywhere;
    }
    .avatar,
    .role {
      background: var(--cf-subtle);
      color: var(--cf-text);
    }
    .avatar {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 34px;
      height: 34px;
      border-radius: 50%;
      font-weight: 700;
    }
    .role {
      display: inline-flex;
      align-items: center;
      height: 22px;
      padding: 0 8px;
      border-radius: 11px;
      font-size: 12px;
      font-weight: 600;
      white-space: nowrap;
    }
    [data-role='Teacher'] {
      background: #f1e8fb;
      color: #5b2a86;
    }
    [data-role='Treasurer'] {
      background: var(--cf-primary-tint);
      color: var(--cf-primary-dark);
    }
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      overflow: hidden;
      clip: rect(0 0 0 0);
    }
  `,
})
export class MembersPage {
  private readonly api = inject(MembersApi);
  private readonly destroyRef = inject(DestroyRef);

  /** `:fundId` trên URL (component input binding, kế thừa tham số route cha). */
  readonly fundId = input.required<string>();

  protected readonly filters = FILTERS;
  protected readonly roleLabel = MEMBER_ROLE_LABEL;
  protected readonly initialOf = initialOf;
  protected readonly filter = signal<RoleFilter>('all');
  protected readonly state = signal<LoadState<Member[]>>(LOADING);

  protected readonly kind = computed(() => stateKind(this.state(), () => false));
  protected readonly members = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.data : null;
  });
  protected readonly rows = computed(() => {
    const filter = this.filter();
    return (this.members() ?? []).filter((member) => filter === 'all' || member.role === filter);
  });
  protected readonly counts = computed<Record<RoleFilter, number>>(() => {
    const all = this.members() ?? [];
    const count = (role: MemberRole) => all.filter((member) => member.role === role).length;
    return { all: all.length, Teacher: count('Teacher'), Treasurer: count('Treasurer'), Student: count('Student') };
  });

  constructor() {
    effect(() => {
      this.fundId();
      untracked(() => this.load());
    });
  }

  protected load(): void {
    toLoadState(this.api.list(this.fundId()))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((state) => this.state.set(state));
  }
}
