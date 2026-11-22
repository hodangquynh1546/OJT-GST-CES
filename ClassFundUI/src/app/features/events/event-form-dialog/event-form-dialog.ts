import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DatePickerComponent } from '@progress/kendo-angular-dateinputs';
import { DialogActionsComponent, DialogComponent, DialogTitleBarComponent } from '@progress/kendo-angular-dialog';
import { ErrorComponent, FormFieldComponent, TextAreaComponent, TextBoxComponent } from '@progress/kendo-angular-inputs';
import { LabelComponent } from '@progress/kendo-angular-label';
import { firstValueFrom } from 'rxjs';

import { AppNotifications } from '../../../core/services/notification.service';
import { applyServerErrors, serverError } from '../../../shared/forms/server-errors';
import { fromApiDate, toApiDate } from '../../../shared/utils/api-date';
import { EventsApi } from '../events.api';
import '../events.errors';
import { EVENT_DESCRIPTION_MAX, EVENT_NAME_MAX, EventDetail } from '../events.models';

/** E-02 Tạo sự kiện (hộp thoại mở từ E-01, cũng mở thẳng bằng `…/events/new`) và sửa thông tin sự kiện (FR-30). */
@Component({
  selector: 'app-event-form-dialog',
  imports: [
    ButtonComponent,
    DatePickerComponent,
    DialogActionsComponent,
    DialogComponent,
    DialogTitleBarComponent,
    ErrorComponent,
    FormFieldComponent,
    LabelComponent,
    ReactiveFormsModule,
    TextAreaComponent,
    TextBoxComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <kendo-dialog [width]="520" [maxWidth]="'calc(100vw - 32px)'" (close)="closed.emit()">
      <kendo-dialog-titlebar>
        <div class="title">
          <span>{{ event() ? 'Sửa sự kiện' : 'Tạo sự kiện' }}</span>
          @if (!event()) {
            <span class="sub">Mọi thành viên đang hoạt động được mời và nhận thông báo.</span>
          }
        </div>
      </kendo-dialog-titlebar>
      <form class="form" [formGroup]="form" (ngSubmit)="save()" novalidate>
        <kendo-formfield class="field cf-required">
          <kendo-label [for]="nameInput" text="Tên sự kiện" />
          <kendo-textbox #nameInput formControlName="name" [maxlength]="nameMax" placeholder="VD: Văn nghệ chào mừng 20/11" />
          <kendo-formerror>{{ serverError(form.controls.name) ?? 'Vui lòng nhập tên sự kiện.' }}</kendo-formerror>
        </kendo-formfield>
        <kendo-formfield class="field">
          <kendo-label [for]="descriptionInput" text="Mô tả" />
          <kendo-textarea #descriptionInput formControlName="description" [rows]="2" resizable="vertical" [maxlength]="descriptionMax" />
        </kendo-formfield>
        <kendo-formfield class="field cf-required date">
          <kendo-label [for]="dateInput" text="Ngày diễn ra" />
          <kendo-datepicker #dateInput formControlName="eventDate" format="dd/MM/yyyy" />
          <kendo-formerror>{{ serverError(form.controls.eventDate) ?? 'Vui lòng chọn ngày diễn ra.' }}</kendo-formerror>
        </kendo-formfield>
        @if (!event()) {
          <div class="cf-alert">Khoản thu của sự kiện được tạo sau, trong trang chi tiết, và chỉ áp cho người đã xác nhận tham gia.</div>
        }
      </form>
      <kendo-dialog-actions layout="end">
        <button kendoButton type="button" (click)="closed.emit()">Hủy</button>
        <button kendoButton themeColor="primary" type="button" [disabled]="busy()" (click)="save()">
          {{ busy() ? 'Đang lưu…' : event() ? 'Lưu thay đổi' : 'Tạo và gửi lời mời' }}
        </button>
      </kendo-dialog-actions>
    </kendo-dialog>
  `,
  styles: `
    .title {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .sub {
      color: var(--cf-text-muted);
      font-size: 13px;
      font-weight: 400;
    }
    .form {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin: 0;
    }
    .date {
      max-width: 240px;
    }
  `,
})
export class EventFormDialog implements OnInit {
  private readonly api = inject(EventsApi);
  private readonly notifications = inject(AppNotifications);

  readonly fundId = input.required<string>();
  /** Có giá trị = sửa sự kiện này. */
  readonly event = input<EventDetail | null>(null);

  readonly closed = output<void>();
  readonly saved = output<EventDetail>();

  protected readonly nameMax = EVENT_NAME_MAX;
  protected readonly descriptionMax = EVENT_DESCRIPTION_MAX;
  protected readonly serverError = serverError;
  protected readonly busy = signal(false);

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(EVENT_NAME_MAX)] }),
    description: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(EVENT_DESCRIPTION_MAX)] }),
    eventDate: new FormControl<Date | null>(null, { validators: [Validators.required] }),
  });

  ngOnInit(): void {
    const current = this.event();
    if (current) {
      this.form.setValue({ name: current.name, description: current.description ?? '', eventDate: fromApiDate(current.eventDate) });
    }
  }

  protected async save(): Promise<void> {
    if (!this.form.controls.name.value.trim()) {
      this.form.controls.name.setErrors({ required: true });
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const body = { name: value.name.trim(), description: value.description.trim() || null, eventDate: toApiDate(value.eventDate!) };
    const current = this.event();
    this.busy.set(true);
    try {
      const result = await firstValueFrom(current ? this.api.update(this.fundId(), current.id, body) : this.api.create(this.fundId(), body));
      this.notifications.success(current ? 'Đã lưu thông tin sự kiện.' : `Đã tạo sự kiện và gửi lời mời tới ${result.invitedCount} thành viên.`);
      this.saved.emit(result);
    } catch (error) {
      const message = applyServerErrors(this.form, error, {});
      if (message) {
        this.notifications.error(message);
      }
    } finally {
      this.busy.set(false);
    }
  }
}
