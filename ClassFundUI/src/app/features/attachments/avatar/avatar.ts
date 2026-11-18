import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, input, output, signal } from '@angular/core';
import { ButtonComponent } from '@progress/kendo-angular-buttons';
import { firstValueFrom } from 'rxjs';

import { ApiError } from '../../../shared/models/api-error';
import { AttachmentsApi } from '../attachments.api';
import '../attachments.errors';
import { FILE_LIMITS, validateFile } from '../attachments.models';

/** "Nguyễn Minh Anh" → "MA" (chữ đầu của hai từ cuối). */
export function avatarInitials(fullName: string): string {
  const words = fullName.trim().split(/\s+/).filter(Boolean);
  return words
    .slice(-2)
    .map((word) => word[0])
    .join('')
    .toUpperCase();
}

/**
 * Ảnh đại diện (FR-04) – thành phần của M5 để M1 gắn vào A-04 (`[editable]="true"`) và header.
 * Không có ảnh (404) thì hiện chữ viết tắt. Ảnh tải qua API, không có URL công khai.
 *
 * `<app-avatar [userId]="user.id" [fullName]="user.fullName" [size]="64" [editable]="true" (changed)="…" />`
 */
@Component({
  selector: 'app-avatar',
  imports: [ButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap">
      <span class="avatar" [style.width.px]="size()" [style.height.px]="size()" [style.font-size.px]="size() * 0.32">
        @if (imageUrl(); as url) {
          <img [src]="url" [alt]="'Ảnh đại diện của ' + fullName()" />
        } @else {
          <span aria-hidden="true">{{ initials() }}</span>
        }
      </span>
      @if (editable()) {
        <div class="actions">
          <input #picker type="file" class="picker" [accept]="accept" (change)="onPicked(picker)" />
          <button kendoButton size="small" type="button" [disabled]="uploading()" (click)="picker.click()">
            {{ uploading() ? 'Đang tải lên…' : 'Đổi ảnh đại diện' }}
          </button>
          <span class="k-form-hint">JPG/PNG/WEBP, tối đa 2 MB</span>
          @if (error(); as message) {
            <span class="k-form-error" role="alert">{{ message }}</span>
          }
        </div>
      }
    </div>
  `,
  styles: `
    .wrap {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 16px;
    }
    .avatar {
      display: grid;
      flex: none;
      place-items: center;
      overflow: hidden;
      border-radius: 50%;
      background: var(--cf-subtle);
      color: var(--cf-text);
      font-weight: 600;
    }
    img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .actions {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 4px;
    }
    .picker {
      display: none;
    }
  `,
})
export class Avatar {
  private readonly api = inject(AttachmentsApi);

  readonly userId = input.required<string>();
  readonly fullName = input('');
  readonly size = input(32);
  readonly editable = input(false);
  /** Phát sau khi đổi ảnh thành công. */
  readonly changed = output<void>();

  protected readonly accept = FILE_LIMITS.Avatar.accept;
  protected readonly initials = computed(() => avatarInitials(this.fullName()));
  protected readonly imageUrl = signal<string | null>(null);
  protected readonly uploading = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly version = signal(0);

  constructor() {
    effect((onCleanup) => {
      const userId = this.userId();
      this.version();
      let cancelled = false;
      onCleanup(() => (cancelled = true));
      this.load(userId).then((url) => {
        if (cancelled) {
          if (url) {
            URL.revokeObjectURL(url);
          }
          return;
        }
        this.setUrl(url);
      });
    });
    inject(DestroyRef).onDestroy(() => this.setUrl(null));
  }

  protected async onPicked(picker: HTMLInputElement): Promise<void> {
    const file = picker.files?.[0];
    picker.value = '';
    if (!file) {
      return;
    }
    this.error.set(validateFile('Avatar', file));
    if (this.error()) {
      return;
    }

    this.uploading.set(true);
    try {
      await firstValueFrom(this.api.uploadAvatar(file));
      this.version.update((value) => value + 1);
      this.changed.emit();
    } catch (error) {
      this.error.set(error instanceof ApiError ? error.message : 'Không tải ảnh lên được.');
    } finally {
      this.uploading.set(false);
    }
  }

  private async load(userId: string): Promise<string | null> {
    try {
      return URL.createObjectURL(await firstValueFrom(this.api.avatar(userId)));
    } catch {
      return null; // Chưa có ảnh (404) → chữ viết tắt.
    }
  }

  private setUrl(url: string | null): void {
    const old = this.imageUrl();
    if (old) {
      URL.revokeObjectURL(old);
    }
    this.imageUrl.set(url);
  }
}
