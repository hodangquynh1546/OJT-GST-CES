import { Component } from '@angular/core';
import { DialogContainerDirective } from '@progress/kendo-angular-dialog';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, DialogContainerDirective],
  // `kendoDialogContainer` là chỗ gắn các hộp thoại mở bằng DialogService (ConfirmDialog); thiếu thì Kendo báo
  // "Cannot attach dialog to the page".
  template: '<router-outlet /><ng-container kendoDialogContainer />',
})
export class App {}
