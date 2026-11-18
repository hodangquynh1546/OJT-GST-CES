import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, computed, inject, input, model, signal, viewChild } from '@angular/core';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { SVGIconComponent } from '@progress/kendo-angular-icons';
import { fileIcon, paperclipIcon, xIcon } from '@progress/kendo-svg-icons';
import { firstValueFrom } from 'rxjs';

import { ApiError } from '../../../shared/models/api-error';
import { AttachmentsApi } from '../attachments.api';
import '../attachments.errors';
import { Attachment, AttachmentKind, FILE_LIMITS, formatFileSize, isImage, validateFile } from '../attachments.models';

interface UploadedItem {
  attachment: Attachment;
  /** Ảnh xem trước tạo từ file trên máy (không tải lại từ server). */
  previewUrl: string | null;
}

let nextId = 0;

/**
 * Ô tải chứng từ dùng chung (M6 minh chứng thanh toán, M7 hóa đơn chi): chọn file → kiểm loại/dung lượng →
 * tải lên ngay → `attachments` chứa các file đã tải (gửi `attachments().map(a => a.id)` kèm giao dịch).
 * Bỏ file khỏi danh sách không xóa trên server: file không gắn sẽ tự dọn sau 24 giờ.
 *
 * `<app-file-upload [fundId]="fundId" kind="Receipt" [(attachments)]="files" [required]="amount > 500000" />`
 */
@Component({
  selector: 'app-file-upload',
  imports: [ButtonComponent, SVGIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="upload" [class.invalid]="error()">
      <input
        #picker
        type="file"
        class="picker"
        [id]="inputId"
        [accept]="limit().accept"
        [multiple]="multiple()"
        [disabled]="disabled() || uploading()"
        (change)="onPicked(picker)"
      />
      <div class="bar">
        <button kendoButton type="button" [disabled]="disabled() || uploading() || full()" (click)="picker.click()">
          <kendo-svgicon [icon]="clipIcon" />
          {{ uploading() ? 'Đang tải lên…' : label() }}
        </button>
        <span class="k-form-hint">
          {{ limit().description }}@if (required()) {<b> · bắt buộc</b>}
        </span>
      </div>

      @if (items().length > 0) {
        <ul class="files">
          @for (item of items(); track item.attachment.id) {
            <li>
              @if (item.previewUrl) {
                <img [src]="item.previewUrl" alt="" />
              } @else {
                <span class="icon"><kendo-svgicon [icon]="docIcon" /></span>
              }
              <span class="file-name" [title]="item.attachment.fileName">{{ item.attachment.fileName }}</span>
              <span class="cf-muted size">{{ size(item.attachment.sizeBytes) }}</span>
              <button
                kendoButton
                fillMode="flat"
                size="small"
                type="button"
                [svgIcon]="removeIcon"
                [attr.aria-label]="'Bỏ ' + item.attachment.fileName"
                [disabled]="disabled()"
                (click)="remove(item)"
              ></button>
            </li>
          }
        </ul>
      }

      @if (error(); as message) {
        <span class="k-form-error" role="alert">{{ message }}</span>
      }
    </div>
  `,
  styles: `
    .upload {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .picker {
      display: none;
    }
    .bar {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    .files {
      display: flex;
      flex-direction: column;
      gap: 6px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    li {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 6px 8px;
      border: 1px solid var(--cf-border);
      border-radius: var(--cf-radius-md);
      background: var(--cf-surface);
    }
    img,
    .icon {
      flex: none;
      width: 36px;
      height: 36px;
      border-radius: var(--cf-radius-sm);
      object-fit: cover;
    }
    .icon {
      display: grid;
      place-items: center;
      background: var(--cf-subtle);
      color: var(--cf-text-muted);
    }
    .file-name {
      flex: 1;
      min-width: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .size {
      font-size: 12px;
    }
  `,
})
export class FileUpload {
  private readonly api = inject(AttachmentsApi);
  private readonly pickerRef = viewChild<ElementRef<HTMLInputElement>>('picker');

  readonly fundId = input.required<string>();
  readonly kind = input.required<Exclude<AttachmentKind, 'Avatar'>>();
  readonly multiple = input(true);
  /** Số file tối đa (mặc định 5). */
  readonly maxFiles = input(5);
  readonly required = input(false);
  readonly disabled = input(false);
  readonly label = input('Đính kèm chứng từ');
  /** File đã tải lên thành công. */
  readonly attachments = model<Attachment[]>([]);

  protected readonly inputId = `cf-file-upload-${nextId++}`;
  protected readonly clipIcon = paperclipIcon;
  protected readonly docIcon = fileIcon;
  protected readonly removeIcon = xIcon;
  protected readonly size = formatFileSize;
  protected readonly uploading = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly previews = signal<Record<string, string>>({});

  protected readonly limit = computed(() => FILE_LIMITS[this.kind()]);
  protected readonly full = computed(() => this.attachments().length >= this.maxFiles());
  protected readonly items = computed<UploadedItem[]>(() =>
    this.attachments().map((attachment) => ({ attachment, previewUrl: this.previews()[attachment.id] ?? null })),
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => Object.values(this.previews()).forEach((url) => URL.revokeObjectURL(url)));
  }

  protected async onPicked(picker: HTMLInputElement): Promise<void> {
    const files = Array.from(picker.files ?? []);
    picker.value = '';
    await this.uploadFiles(files);
  }

  /** Tải lên các file đã chọn (cũng dùng cho kéo thả / test). */
  async uploadFiles(files: File[]): Promise<void> {
    this.error.set(null);
    const room = this.maxFiles() - this.attachments().length;
    if (files.length > room) {
      this.error.set(`Tối đa ${this.maxFiles()} file.`);
      files = files.slice(0, Math.max(room, 0));
    }

    for (const file of files) {
      const invalid = validateFile(this.kind(), file);
      if (invalid) {
        this.error.set(`${file.name}: ${invalid}`);
        continue;
      }

      this.uploading.set(true);
      try {
        const attachment = await firstValueFrom(this.api.upload(this.fundId(), this.kind(), file));
        if (isImage(attachment.contentType)) {
          this.previews.update((map) => ({ ...map, [attachment.id]: URL.createObjectURL(file) }));
        }
        this.attachments.update((list) => [...list, attachment]);
      } catch (error) {
        this.error.set(`${file.name}: ${error instanceof ApiError ? error.message : 'Không tải lên được.'}`);
      } finally {
        this.uploading.set(false);
      }
    }
  }

  protected remove(item: UploadedItem): void {
    if (item.previewUrl) {
      URL.revokeObjectURL(item.previewUrl);
    }
    this.attachments.update((list) => list.filter((attachment) => attachment.id !== item.attachment.id));
    this.error.set(null);
  }

  /** Mở hộp chọn file (cho nút bên ngoài). */
  open(): void {
    this.pickerRef()?.nativeElement.click();
  }
}
