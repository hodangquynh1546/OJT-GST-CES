import { ComponentRef } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { AppState, AppStateKind } from './app-state';

describe('AppState', () => {
  let fixture: ComponentFixture<AppState>;
  let ref: ComponentRef<AppState>;

  const render = (kind: AppStateKind) => {
    ref.setInput('kind', kind);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  beforeEach(() => {
    fixture = TestBed.createComponent(AppState);
    ref = fixture.componentRef;
  });

  it('loading hiện skeleton và role status', () => {
    const element = render('loading');
    expect(element.querySelector('[role="status"]')).not.toBeNull();
    expect(element.querySelector('kendo-skeleton')).not.toBeNull();
  });

  it('error dùng role alert với nội dung mặc định', () => {
    const element = render('error');
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Không tải được dữ liệu');
  });

  it('forbidden hiện mã 403', () => {
    expect(render('forbidden').textContent).toContain('403');
  });

  it('nút hành động phát sự kiện action', () => {
    ref.setInput('actionLabel', 'Thử lại');
    const element = render('error');
    const emitted = jasmine.createSpy();
    fixture.componentInstance.action.subscribe(emitted);

    element.querySelector('button')?.click();

    expect(emitted).toHaveBeenCalled();
  });
});
