import { Injectable, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, Router } from '@angular/router';
import { filter, map, startWith } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { FundMembership } from '../auth/auth.models';
import { Permission } from '../auth/permissions';

/** Quỹ đang mở, suy ra từ URL. Chỉ dùng để vẽ giao diện; quyền thật do backend quyết định. */
@Injectable({ providedIn: 'root' })
export class CurrentFund {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  /** Quỹ mở gần nhất (chỉ để hiển thị). */
  private lastFundId: string | null = null;

  private readonly location = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      startWith(null),
      map(() => {
        const fundId = findFundId(this.router.routerState.snapshot.root);
        this.lastFundId = fundId ?? this.lastFundId;
        return { url: this.router.url.split(/[?#]/)[0], fundId, lastFundId: this.lastFundId };
      }),
    ),
    { initialValue: { url: '', fundId: null, lastFundId: null } },
  );

  /** Mã quỹ trên URL (`/funds/:fundId/…`); null khi trang không thuộc quỹ nào. */
  readonly fundId = computed(() => this.location().fundId);

  readonly membership = computed(() => this.auth.membership(this.fundId()));
  readonly permissions = computed<readonly Permission[]>(() => this.membership()?.permissions ?? []);

  /**
   * Quỹ hiển thị ở AppHeader / AppNav (board AppHeader, Account trên Canvas): quỹ trên URL; ở trang ngoài quỹ
   * (Tài khoản, 403, 404) là quỹ mở gần nhất, nếu chưa mở quỹ nào thì quỹ đầu tiên. Trang "Quỹ của tôi / của con"
   * (`/funds`) không gắn quỹ nào.
   */
  readonly context = computed<FundMembership | undefined>(() => {
    const current = this.membership();
    if (current || this.location().url === '/funds') {
      return current;
    }
    return this.auth.membership(this.location().lastFundId) ?? this.auth.user()?.memberships[0];
  });

  can(permission: Permission): boolean {
    return this.permissions().includes(permission);
  }
}

function findFundId(route: ActivatedRouteSnapshot): string | null {
  return route.paramMap.get('fundId') ?? (route.firstChild ? findFundId(route.firstChild) : null);
}
