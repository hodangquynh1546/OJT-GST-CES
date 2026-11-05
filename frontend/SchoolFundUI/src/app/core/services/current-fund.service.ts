import { Injectable, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRouteSnapshot, NavigationEnd, Router } from '@angular/router';
import { filter, map, startWith } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { Permission } from '../auth/permissions';

/** Quỹ đang mở, suy ra từ URL. Chỉ dùng để vẽ giao diện; quyền thật do backend quyết định. */
@Injectable({ providedIn: 'root' })
export class CurrentFund {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly fundId = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      startWith(null),
      map(() => findFundId(this.router.routerState.snapshot.root)),
    ),
    { initialValue: null },
  );

  readonly membership = computed(() => this.auth.membership(this.fundId()));
  readonly permissions = computed<readonly Permission[]>(() => this.membership()?.permissions ?? []);

  can(permission: Permission): boolean {
    return this.permissions().includes(permission);
  }
}

function findFundId(route: ActivatedRouteSnapshot): string | null {
  return route.paramMap.get('fundId') ?? (route.firstChild ? findFundId(route.firstChild) : null);
}
