import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CredentialDialog, CredentialNotice } from './credential-dialog';

const NOTICE: CredentialNotice = {
  title: 'Tài khoản học sinh – Phạm Đức Huy',
  recipient: 'học sinh',
  loginLabel: 'Tên đăng nhập',
  login: 'huy.pham.482',
  password: 'Abcd-efgh-2345-mnpq',
};

describe('CredentialDialog', () => {
  let fixture: ComponentFixture<CredentialDialog>;
  let closed: number;

  const host = () => fixture.nativeElement as HTMLElement;
  const closeButton = () =>
    Array.from(host().querySelectorAll<HTMLButtonElement>('button')).find((button) => button.textContent?.trim() === 'Đóng')!;
  const checkbox = () => host().querySelector<HTMLInputElement>('input[type="checkbox"]')!;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [CredentialDialog], providers: [provideNoopAnimations()] }).compileComponents();
    fixture = TestBed.createComponent(CredentialDialog);
    fixture.componentRef.setInput('notice', NOTICE);
    closed = 0;
    fixture.componentInstance.closed.subscribe(() => closed++);
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('shows the login and the one-time password with the "no email" warning', () => {
    const text = host().textContent ?? '';
    expect(text).toContain('Chỉ hiển thị một lần');
    expect(text).toContain('không gửi email');
    expect(host().querySelector('[data-testid="credential-login"]')?.textContent).toContain('huy.pham.482');
    expect(host().querySelector('[data-testid="credential-password"]')?.textContent).toContain('Abcd-efgh-2345-mnpq');
  });

  it('cannot be closed before the acknowledgement is ticked – not by the button, nor by Esc / ✕', async () => {
    expect(closeButton().disabled).toBeTrue();

    closeButton().click();
    (fixture.componentInstance as unknown as { tryClose(): void }).tryClose();
    expect(closed).toBe(0);

    checkbox().click();
    fixture.detectChanges();
    expect(closeButton().disabled).toBeFalse();

    closeButton().click();
    expect(closed).toBe(1);
  });
});
