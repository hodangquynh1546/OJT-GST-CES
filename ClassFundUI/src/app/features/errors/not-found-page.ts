import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import { AppState } from '../../shared/components/app-state/app-state';

@Component({
  selector: 'app-not-found-page',
  imports: [AppState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-state kind="not-found" actionLabel="Về trang chính" (action)="goHome()" />`,
})
export class NotFoundPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected goHome(): void {
    void this.router.navigateByUrl(this.auth.homeUrl());
  }
}
