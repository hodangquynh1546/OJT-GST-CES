import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DatePickerComponent } from '@progress/kendo-angular-dateinputs';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { DropDownListComponent } from '@progress/kendo-angular-dropdowns';
import { CellTemplateDirective, ColumnComponent, GridComponent, NoRecordsTemplateDirective } from '@progress/kendo-angular-grid';
import { CheckBoxComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { catchError, map, of, switchMap } from 'rxjs';

import { AppState } from '../../../shared/components/app-state/app-state';
import { ScopeBadge } from '../../../shared/components/scope-badge/scope-badge';
import { PagedResult } from '../../../shared/models/paged-result';
import { LOADING, LoadState, stateKind, toLoadState } from '../../../shared/state/load-state';
import { EventsApi } from '../../events/events.api';
import { EventSummary } from '../../events/events.models';
import { AuditApi } from '../audit.api';
import { AUDIT_ACTION_LABEL, actionLabel, auditChanges, defaultRange, formatAuditTime, toApiDate } from '../audit-format';
import { AUDIT_PAGE_SIZE, AuditActor, AuditLog, AuditScopeFilter } from '../audit.models';

const ALL_ACTORS: AuditActor = { userId: '', fullName: 'Tất cả' };
const ALL_EVENTS = { id: '', name: 'Mọi sự kiện' };
const ALL_ACTIONS = { code: '', label: 'Mọi hành động' };

/** Mã hành động có nhãn tiếng Việt, xếp theo nhãn (bộ lọc "Hành động", FR-47). */
const ACTION_OPTIONS = Object.entries(AUDIT_ACTION_LABEL)
  .map(([code, label]) => ({ code, label }))
  .sort((a, b) => a.label.localeCompare(b.label, 'vi'));

/** U-01 · Nhật ký hoạt động (FR-45 → FR-47): xem, lọc, trước–sau. Chỉ đọc. Theo canvas Audit.dc.html. */
@Component({
  selector: 'app-audit-log-page',
  imports: [
    AppState,
    ButtonComponent,
    CellTemplateDirective,
    CheckBoxComponent,
    ColumnComponent,
    DatePickerComponent,
    DialogActionsComponent,
    DialogComponent,
    DropDownListComponent,
    FormsModule,
    GridComponent,
    LabelComponent,
    NoRecordsTemplateDirective,
    ScopeBadge,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <div class="head-t">
        <h1>Nhật ký hoạt động</h1>
        <p class="cf-muted">Mọi thao tác ghi: ai, khi nào, hành động, phạm vi, trước–sau. Chỉ ghi thêm, không sửa/xóa.</p>
      </div>

      <section class="cf-card filters" aria-label="Bộ lọc nhật ký">
        <div class="field date">
          <kendo-label [for]="fromInput" text="Từ ngày" />
          <kendo-datepicker
            #fromInput
            format="dd/MM/yyyy"
            [max]="maxFrom()"
            [ngModel]="from()"
            (ngModelChange)="setFrom($event)"
          />
        </div>
        <div class="field date">
          <kendo-label [for]="toInput" text="Đến ngày" />
          <kendo-datepicker
            #toInput
            format="dd/MM/yyyy"
            [min]="minTo()"
            [ngModel]="to()"
            (ngModelChange)="setTo($event)"
          />
        </div>
        <div class="field actor">
          <kendo-label [for]="actorInput" text="Người thực hiện" />
          <kendo-dropdownlist
            #actorInput
            [data]="actors()"
            textField="fullName"
            valueField="userId"
            [valuePrimitive]="true"
            [defaultItem]="allActors"
            [ngModel]="actorId()"
            (ngModelChange)="setActor($event)"
          />
        </div>
        <div class="field actor">
          <kendo-label [for]="eventInput" text="Sự kiện" />
          <kendo-dropdownlist
            #eventInput
            [data]="events()"
            textField="name"
            valueField="id"
            [valuePrimitive]="true"
            [defaultItem]="allEvents"
            [ngModel]="eventId()"
            (ngModelChange)="setEvent($event)"
          />
        </div>
        <div class="field actor">
          <kendo-label [for]="actionInput" text="Hành động" />
          <kendo-dropdownlist
            #actionInput
            [data]="actionOptions"
            textField="label"
            valueField="code"
            [valuePrimitive]="true"
            [defaultItem]="allActions"
            [ngModel]="action()"
            (ngModelChange)="setAction($event)"
          />
        </div>
        <div class="field scope">
          <span class="k-label" id="scope-label">Phạm vi</span>
          <div class="tabs" role="radiogroup" aria-labelledby="scope-label">
            @for (option of scopeOptions; track option.value) {
              <button
                type="button"
                role="radio"
                class="tab"
                [class.on]="scope() === option.value"
                [attr.aria-checked]="scope() === option.value"
                (click)="setScope(option.value)"
              >
                {{ option.label }}
              </button>
            }
          </div>
        </div>
        <div class="check">
          <kendo-checkbox #selfInput [ngModel]="selfOnly()" (ngModelChange)="setSelfOnly($event)" />
          <kendo-label [for]="selfInput" text="Chỉ thao tác tự ghi nhận" />
        </div>
      </section>

      <section class="cf-card list" aria-label="Danh sách nhật ký">
        @if (errorKind(); as kind) {
          <app-state [kind]="kind" />
        } @else if (data(); as result) {
          <kendo-grid [data]="result.items" [loading]="loading()" class="audit-grid">
            <kendo-grid-column title="Thời điểm" [width]="120">
              <ng-template kendoGridCellTemplate let-row>
                <span class="cf-muted nowrap">{{ time(row.at) }}</span>
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Người" [width]="170" field="actorName" />
            <kendo-grid-column title="Hành động" [width]="230">
              <ng-template kendoGridCellTemplate let-row>
                <div class="title-row">
                  <span class="action" [title]="row.action">{{ label(row.action) }}</span>
                  @if (row.isSelfAction) {
                    <span class="cf-tag cf-tag--warning">Tự ghi nhận</span>
                  }
                </div>
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Phạm vi" [width]="230">
              <ng-template kendoGridCellTemplate let-row>
                <app-scope-badge [scope]="row.scope" [eventName]="row.eventName" />
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column title="Chi tiết">
              <ng-template kendoGridCellTemplate let-row>
                <span class="small">{{ row.summary || '–' }}</span>
              </ng-template>
            </kendo-grid-column>
            <kendo-grid-column [width]="130">
              <ng-template kendoGridCellTemplate let-row>
                <div class="right">
                  <button kendoButton fillMode="outline" size="small" type="button" (click)="open(row)">Trước / sau</button>
                </div>
              </ng-template>
            </kendo-grid-column>
            <ng-template kendoGridNoRecordsTemplate>
              <app-state kind="empty-filter" title="Không có thao tác phù hợp" message="Thử đổi khoảng ngày, người thực hiện hoặc phạm vi." />
            </ng-template>
          </kendo-grid>
          <div class="card-f">
            <span class="cf-muted small">
              {{ result.total }} bản ghi@if (pageCount() > 1) {<span> · trang {{ page() }}/{{ pageCount() }}</span>}
            </span>
            <div class="actions">
              <button kendoButton fillMode="outline" size="small" type="button" [disabled]="page() <= 1 || loading()" (click)="go(-1)">
                Trước
              </button>
              <button kendoButton fillMode="outline" size="small" type="button" [disabled]="page() >= pageCount() || loading()" (click)="go(1)">
                Sau
              </button>
            </div>
          </div>
        } @else {
          <app-state kind="loading" />
        }
      </section>
    </div>

    @if (selected(); as row) {
      <kendo-dialog [title]="label(row.action)" [width]="640" [maxWidth]="'calc(100vw - 32px)'" (close)="close()">
        <p class="cf-muted small meta">{{ row.actorName }} · {{ time(row.at) }}@if (row.ipAddress) {<span> · IP {{ row.ipAddress }}</span>}</p>
        @if (row.note) {
          <p class="small note">Ghi chú: {{ row.note }}</p>
        }
        @let diff = changes(row);
        @if (diff.rows.length) {
          <table class="diff" aria-label="So sánh trước và sau">
            <thead>
              <tr>
                <th scope="col">Thông tin</th>
                @if (diff.hasBefore) {
                  <th scope="col">Trước</th>
                }
                @if (diff.hasAfter) {
                  <th scope="col">{{ diff.hasBefore ? 'Sau' : 'Giá trị ghi nhận' }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (change of diff.rows; track $index) {
                <tr [class.changed]="change.changed">
                  <th scope="row">{{ change.label }}</th>
                  @if (diff.hasBefore) {
                    <td class="before">{{ change.before }}</td>
                  }
                  @if (diff.hasAfter) {
                    <td class="after">
                      {{ change.after }}
                      @if (change.changed) {
                        <span class="cf-tag cf-tag--success">Đã đổi</span>
                      }
                    </td>
                  }
                </tr>
              }
            </tbody>
          </table>
        } @else {
          <p class="cf-muted small">Thao tác này không lưu dữ liệu trước / sau.</p>
        }
        <kendo-dialog-actions layout="end">
          <button kendoButton themeColor="primary" type="button" (click)="close()">Đóng</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }
  `,
  // Theo canvas U-01 (Audit.dc.html + cf.css); màu dùng token --cf-*.
  styles: `
    .page {
      display: flex;
      flex-direction: column;
      gap: 24px;
      max-width: var(--cf-content-max);
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
    .nowrap {
      white-space: nowrap;
    }
    .filters {
      display: flex;
      flex-wrap: wrap;
      align-items: flex-end;
      gap: 12px;
      padding: 20px;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .date {
      flex: 1 1 150px;
    }
    .actor,
    .scope {
      flex: 1 1 180px;
    }
    .tabs {
      display: inline-flex;
      flex-wrap: wrap;
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
    /* Canvas .check.small: ô chọn 16 px + chữ 13 px thường, căn giữa theo hàng ô lọc. */
    .check {
      display: flex;
      flex: 1 1 200px;
      align-items: center;
      gap: 10px;
      min-height: 36px;
    }
    .check ::ng-deep .k-label {
      margin: 0;
      font-size: 13px;
      font-weight: 400;
    }
    .check ::ng-deep .k-checkbox {
      margin: 0;
    }
    .list {
      padding: 0;
      overflow: hidden;
    }
    .title-row {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .action {
      font-weight: 500;
    }
    .right {
      display: flex;
      justify-content: flex-end;
    }
    .card-f {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
      padding: 12px 16px;
      border-top: 1px solid var(--cf-border);
    }
    .actions {
      display: flex;
      gap: 8px;
    }
    .meta {
      margin-bottom: 16px;
    }
    .note {
      margin-bottom: 16px;
    }
    /* Bảng so sánh: cột "Trước" nền xám, "Sau" nền xanh (giữ màu hai khung trước–sau của canvas). */
    .diff {
      width: 100%;
      border-collapse: separate;
      border-spacing: 0;
      border: 1px solid var(--cf-border);
      border-radius: var(--cf-radius-lg);
      overflow: hidden;
      font-size: 13px;
    }
    .diff th,
    .diff td {
      padding: 10px 12px;
      border-bottom: 1px solid var(--cf-border);
      text-align: left;
      vertical-align: top;
      word-break: break-word;
    }
    .diff tbody tr:last-child th,
    .diff tbody tr:last-child td {
      border-bottom: 0;
    }
    .diff thead th {
      background: var(--cf-surface);
      color: var(--cf-text-muted);
      font-weight: 500;
    }
    .diff tbody th {
      width: 34%;
      color: var(--cf-text-muted);
      font-weight: 500;
    }
    .diff td.before {
      background: var(--cf-background);
    }
    .diff td.after {
      background: var(--cf-success-bg);
    }
    .diff tr.changed td.before {
      color: var(--cf-text-muted);
      text-decoration: line-through;
    }
    .diff tr.changed td.after {
      font-weight: 600;
    }
    .diff .cf-tag {
      margin-left: 6px;
      font-weight: 500;
      vertical-align: middle;
    }
  `,
})
export class AuditLogPage {
  private readonly api = inject(AuditApi);
  private readonly route = inject(ActivatedRoute);
  private readonly eventsApi = inject(EventsApi);

  protected readonly allActors = ALL_ACTORS;
  protected readonly allEvents = ALL_EVENTS;
  protected readonly allActions = ALL_ACTIONS;
  protected readonly actionOptions = ACTION_OPTIONS;
  protected readonly scopeOptions: readonly { value: AuditScopeFilter; label: string }[] = [
    { value: 'all', label: 'Tất cả' },
    { value: 'General', label: 'Chung' },
    { value: 'Event', label: 'Sự kiện' },
  ];
  protected readonly label = actionLabel;
  protected readonly time = formatAuditTime;
  protected readonly changes = (row: AuditLog) => auditChanges(row.before, row.after);

  private readonly range = defaultRange();
  protected readonly from = signal<Date | null>(this.range.from);
  protected readonly to = signal<Date | null>(this.range.to);
  protected readonly actorId = signal<string>('');
  protected readonly scope = signal<AuditScopeFilter>('all');
  protected readonly eventId = signal<string>('');
  protected readonly action = signal<string>('');
  protected readonly selfOnly = signal(false);
  protected readonly page = signal(1);
  protected readonly selected = signal<AuditLog | null>(null);
  /** "Đến ngày" không trước "Từ ngày" (API cũng trả 400). */
  protected readonly maxFrom = computed(() => this.to() ?? new Date(2100, 0, 1));
  protected readonly minTo = computed(() => this.from() ?? new Date(2000, 0, 1));

  private readonly fundId = toSignal(this.route.paramMap.pipe(map((params) => params.get('fundId') ?? '')), {
    initialValue: '',
  });

  protected readonly actors = toSignal(
    toObservable(this.fundId).pipe(switchMap((fundId) => this.api.actors(fundId).pipe(catchError(() => of<AuditActor[]>([]))))),
    { initialValue: [] as AuditActor[] },
  );

  protected readonly events = toSignal(
    toObservable(this.fundId).pipe(
      switchMap((fundId) => (fundId ? this.eventsApi.list(fundId).pipe(catchError(() => of<EventSummary[]>([]))) : of<EventSummary[]>([]))),
    ),
    { initialValue: [] as EventSummary[] },
  );

  private readonly query = computed(() => ({
    fundId: this.fundId(),
    from: toApiDate(this.from()),
    to: toApiDate(this.to()),
    actorId: this.actorId() || null,
    scope: this.scope(),
    eventId: this.eventId() || null,
    action: this.action() || null,
    selfOnly: this.selfOnly(),
    page: this.page(),
    pageSize: AUDIT_PAGE_SIZE,
  }));

  private readonly state = toSignal(
    toObservable(this.query).pipe(switchMap((query) => toLoadState(this.api.list(query.fundId, query)))),
    { initialValue: LOADING as LoadState<PagedResult<AuditLog>> },
  );

  /** Dữ liệu gần nhất: giữ bảng cũ khi đang tải trang / bộ lọc mới (Grid hiện vòng tải). */
  protected readonly data = signal<PagedResult<AuditLog> | null>(null);
  protected readonly loading = computed(() => this.state().status === 'loading');
  protected readonly errorKind = computed(() => {
    const current = this.state();
    return current.status === 'error' ? stateKind(current) : null;
  });
  protected readonly pageCount = computed(() => Math.max(1, Math.ceil((this.data()?.total ?? 0) / AUDIT_PAGE_SIZE)));

  constructor() {
    effect(() => {
      const current = this.state();
      if (current.status === 'ready') {
        this.data.set(current.data);
      }
    });
  }

  protected setFrom(value: Date | null): void {
    this.from.set(value);
    this.page.set(1);
  }

  protected setTo(value: Date | null): void {
    this.to.set(value);
    this.page.set(1);
  }

  protected setActor(value: string | null): void {
    this.actorId.set(value ?? '');
    this.page.set(1);
  }

  protected setEvent(value: string | null): void {
    this.eventId.set(value ?? '');
    this.page.set(1);
  }

  protected setAction(value: string | null): void {
    this.action.set(value ?? '');
    this.page.set(1);
  }

  protected setScope(value: AuditScopeFilter): void {
    this.scope.set(value);
    this.page.set(1);
  }

  protected setSelfOnly(value: boolean): void {
    this.selfOnly.set(!!value);
    this.page.set(1);
  }

  protected go(step: number): void {
    this.page.update((current) => Math.min(Math.max(1, current + step), this.pageCount()));
  }

  protected open(row: AuditLog): void {
    this.selected.set(row);
  }

  protected close(): void {
    this.selected.set(null);
  }
}
