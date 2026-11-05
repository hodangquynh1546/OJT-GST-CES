import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';

import { Permission } from '../../core/auth/permissions';
import { CurrentFund } from '../../core/services/current-fund.service';

/** Không có quyền thì không vẽ phần tử: `*appHasPermission="Permissions.TransactionManage"`. */
@Directive({ selector: '[appHasPermission]' })
export class HasPermissionDirective {
  private readonly template = inject(TemplateRef<unknown>);
  private readonly container = inject(ViewContainerRef);
  private readonly currentFund = inject(CurrentFund);

  readonly appHasPermission = input.required<Permission>();

  constructor() {
    effect(() => {
      this.container.clear();
      if (this.currentFund.can(this.appHasPermission())) {
        this.container.createEmbeddedView(this.template);
      }
    });
  }
}
