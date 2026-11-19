import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal } from '@angular/core';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { DialogActionsComponent, DialogComponent } from '@progress/kendo-angular-dialog';
import { firstValueFrom } from 'rxjs';

import { AppNotifications } from '../../../core/services/notification.service';
import { AttachmentsApi, saveBlob } from '../../attachments/attachments.api';
import { Attachment, formatFileSize, isImage } from '../../attachments/attachments.models';

/**
 * Lưới chứng từ của một giao dịch (L-03 "Chứng từ (n)", canvas `.proof`): ảnh hiện thu nhỏ, file khác hiện tên + dung lượng;
 * bấm để xem lớn / tải xuống. File luôn tải qua API (kiểm quyền AZ-05), không có link trực tiếp tới object storage.
 */
@Component({
  selector: 'app-attachment-gallery',
  imports: [ButtonComponent, DialogActionsComponent, DialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="grid">
      @for (file of attachments(); track file.id) {
        <button type="button" class="proof" [attr.aria-label]="'Xem ' + file.fileName" (click)="open(file)">
          @if (previews()[file.id]; as url) {
            <img [src]="url" alt="" />
          } @else {
            <span>[{{ file.fileName }} · {{ size(file.sizeBytes) }}]</span>
          }
        </button>
      }
    </div>

    @if (viewing(); as file) {
      <kendo-dialog [title]="file.fileName" [width]="640" [maxWidth]="'calc(100vw - 32px)'" (close)="viewing.set(null)">
        <div class="proof big">
          @if (previews()[file.id]; as url) {
            <img [src]="url" [alt]="file.fileName" />
          } @else {
            <span>{{ file.fileName }} · {{ size(file.sizeBytes) }}<br />Không xem trước được – tải xuống để mở.</span>
          }
        </div>
        <kendo-dialog-actions layout="end">
          <button kendoButton type="button" (click)="download(file)">Tải xuống</button>
          <button kendoButton themeColor="primary" type="button" (click)="viewing.set(null)">Đóng</button>
        </kendo-dialog-actions>
      </kendo-dialog>
    }
  `,
  styles: `
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
      gap: 12px;
    }
    /* Canvas .proof: khung kẻ sọc nhạt, chữ xám giữa. */
    .proof {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 140px;
      padding: 12px;
      overflow: hidden;
      border: 1px solid var(--cf-border);
      border-radius: var(--cf-radius-lg);
      background: repeating-linear-gradient(45deg, var(--cf-background), var(--cf-background) 10px, var(--cf-subtle) 10px, var(--cf-subtle) 20px);
      color: var(--cf-text-muted);
      font: inherit;
      font-size: 13px;
      text-align: center;
      cursor: pointer;
    }
    .proof img {
      max-width: 100%;
      max-height: 100%;
      object-fit: contain;
    }
    .proof.big {
      height: 360px;
      cursor: default;
    }
  `,
})
export class AttachmentGallery {
  private readonly api = inject(AttachmentsApi);
  private readonly notifications = inject(AppNotifications);

  readonly fundId = input.required<string>();
  readonly attachments = input.required<Attachment[]>();

  protected readonly previews = signal<Record<string, string>>({});
  protected readonly viewing = signal<Attachment | null>(null);
  protected readonly size = formatFileSize;
  private readonly blobs = new Map<string, Blob>();

  constructor() {
    effect(() => {
      const fundId = this.fundId();
      for (const file of this.attachments()) {
        if (isImage(file.contentType) && !this.blobs.has(file.id)) {
          void this.load(fundId, file);
        }
      }
    });
    inject(DestroyRef).onDestroy(() => Object.values(this.previews()).forEach((url) => URL.revokeObjectURL(url)));
  }

  protected open(file: Attachment): void {
    this.viewing.set(file);
  }

  protected async download(file: Attachment): Promise<void> {
    try {
      const blob = this.blobs.get(file.id) ?? (await firstValueFrom(this.api.download(this.fundId(), file.id)));
      saveBlob(blob, file.fileName);
    } catch {
      this.notifications.error('Không tải được chứng từ. Vui lòng thử lại.');
    }
  }

  private async load(fundId: string, file: Attachment): Promise<void> {
    try {
      const blob = await firstValueFrom(this.api.download(fundId, file.id));
      this.blobs.set(file.id, blob);
      this.previews.update((map) => ({ ...map, [file.id]: URL.createObjectURL(blob) }));
    } catch {
      // Không có ảnh thu nhỏ thì hiện tên file; bấm vẫn tải được.
    }
  }
}
