import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { fundShortName } from '../../core/navigation/fund-short-name';
import { ROLE_DISPLAY } from '../../core/navigation/role-display';
import { TopBar } from '../account/top-bar';

/**
 * F-01 Quỹ của tôi (FR-08): màn đầu tiên sau đăng nhập, chưa có menu quỹ. Chọn một quỹ mới vào khung có menu.
 * Hiện tên quỹ + vai trò (tính riêng từng quỹ) từ `GET /auth/me`; số liệu trên thẻ (số dư, chờ xác nhận, số thành
 * viên, năm học) và nút Tạo quỹ thật do module M2 bổ sung.
 */
@Component({
  selector: 'app-fund-list-page',
  imports: [RouterLink, TopBar],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-top-bar variant="funds" />
    <main class="page">
      <div class="head">
        <div>
          <h1>Quỹ của tôi</h1>
          <p class="subtitle">Các quỹ bạn đang tham gia. Vai trò được tính riêng cho từng quỹ.</p>
        </div>
        @if (canCreateFund()) {
          <a routerLink="/funds/new" class="create">+ Tạo quỹ</a>
        }
      </div>

      @if (funds().length > 0) {
        <ul class="grid" aria-label="Danh sách quỹ">
          @for (fund of funds(); track fund.fundId) {
            <li>
              <a class="card" [routerLink]="['/funds', fund.fundId]" [attr.aria-label]="'Mở quỹ ' + fund.fundName">
                <span class="logo" aria-hidden="true">{{ fund.short }}</span>
                <span class="text">
                  <b class="name">{{ fund.fundName }}</b>
                </span>
                <span class="badge" [class]="'badge badge--' + fund.role">{{ fund.display.badge }}</span>
              </a>
            </li>
          }
        </ul>
      } @else {
        <section class="empty" aria-labelledby="empty-title">
          <b id="empty-title">Bạn chưa thuộc quỹ nào</b>
          <p>Thủ quỹ sẽ thêm bạn bằng email, hoặc tự tạo quỹ nếu bạn đăng ký làm thủ quỹ.</p>
          @if (canCreateFund()) {
            <a routerLink="/funds/new" class="cf-link-button">Tạo quỹ đầu tiên</a>
          }
        </section>
      }
    </main>
  `,
  styles: `
    :host {
      display: block;
      min-height: 100dvh;
      background: var(--cf-background);
    }
    .page {
      box-sizing: border-box;
      width: 100%;
      max-width: 1152px;
      margin: 0 auto;
      padding: 32px 24px 48px;
    }
    .head {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 16px;
      margin-bottom: 24px;
    }
    h1 {
      font-size: 24px;
    }
    .subtitle {
      margin: 4px 0 0;
      color: var(--cf-text-muted);
    }
    .create {
      display: inline-flex;
      align-items: center;
      min-height: 40px;
      padding: 0 16px;
      border-radius: var(--cf-radius-lg);
      background: var(--cf-ink);
      color: var(--cf-surface);
      font-weight: 600;
      text-decoration: none;
    }
    .create:hover {
      background: var(--cf-ink-hover);
      color: var(--cf-surface);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
      gap: 16px;
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .card {
      display: flex;
      align-items: center;
      gap: 12px;
      box-sizing: border-box;
      padding: 20px;
      border: 1px solid var(--cf-border);
      border-radius: 12px;
      background: var(--cf-surface);
      color: var(--cf-text);
      text-decoration: none;
    }
    .card:hover {
      border-color: var(--cf-border-strong);
      box-shadow: 0 1px 3px rgba(15, 23, 42, 0.08);
      color: var(--cf-text);
    }
    .logo {
      display: flex;
      flex: none;
      align-items: center;
      justify-content: center;
      width: 40px;
      height: 40px;
      border-radius: 8px;
      background: var(--cf-ink);
      color: var(--cf-surface);
      font-size: 11px;
      font-weight: 700;
    }
    .text {
      flex: 1;
      min-width: 0;
    }
    .name {
      display: block;
      overflow: hidden;
      font-size: 16px;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .badge {
      flex: none;
      padding: 0 10px;
      border-radius: var(--cf-radius-pill);
      font-size: 12px;
      font-weight: 600;
      line-height: 24px;
    }
    .badge--Owner {
      background: var(--cf-ink);
      color: var(--cf-surface);
    }
    .badge--Treasurer {
      background: var(--cf-role-treasurer-tint);
      color: var(--cf-role-treasurer);
    }
    .badge--Student {
      background: var(--cf-subtle);
      color: var(--cf-text);
    }
    .empty {
      padding: 32px 16px;
      border: 1px dashed var(--cf-border-strong);
      border-radius: 12px;
      text-align: center;
    }
    .empty p {
      margin: 8px 0 16px;
      color: var(--cf-text-muted);
    }
  `,
})
export class FundListPage {
  private readonly auth = inject(AuthService);

  protected readonly canCreateFund = this.auth.canCreateFund;
  protected readonly funds = computed(() =>
    (this.auth.user()?.memberships ?? []).map((item) => ({
      ...item,
      display: ROLE_DISPLAY[item.role],
      short: fundShortName(item.fundName),
    })),
  );
}
