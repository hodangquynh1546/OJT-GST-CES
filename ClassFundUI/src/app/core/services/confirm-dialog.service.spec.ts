import { TestBed } from '@angular/core/testing';
import { DialogCloseResult, DialogService } from '@progress/kendo-angular-dialog';
import { Subject, firstValueFrom } from 'rxjs';

import { ConfirmDialog } from './confirm-dialog.service';

describe('ConfirmDialog', () => {
  let result: Subject<unknown>;
  let options: { actions: { text: string; themeColor?: string }[]; width: number };

  beforeEach(() => {
    result = new Subject();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: DialogService,
          useValue: {
            open: (value: typeof options) => {
              options = value;
              return { result };
            },
          },
        },
      ],
    });
  });

  it('true khi bấm nút xác nhận, nút đỏ khi là hành động phá hủy', async () => {
    const answer = firstValueFrom(
      TestBed.inject(ConfirmDialog).confirm({ title: 'Thu hồi', message: 'Chắc chắn?', confirmLabel: 'Thu hồi', danger: true }),
    );
    expect(options.width).toBe(480);
    expect(options.actions.map((action) => action.text)).toEqual(['Hủy bỏ', 'Thu hồi']);
    expect(options.actions[1].themeColor).toBe('error');
    result.next(options.actions[1]);
    expect(await answer).toBeTrue();
  });

  it('false khi đóng hoặc bấm Hủy bỏ', async () => {
    const service = TestBed.inject(ConfirmDialog);
    const closed = firstValueFrom(service.confirm({ title: 't', message: 'm', confirmLabel: 'OK' }));
    result.next(new DialogCloseResult());
    expect(await closed).toBeFalse();

    const cancelled = firstValueFrom(service.confirm({ title: 't', message: 'm', confirmLabel: 'OK' }));
    result.next(options.actions[0]);
    expect(await cancelled).toBeFalse();
  });
});
