import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl } from '@angular/forms';
import { map, startWith, switchMap } from 'rxjs';

import { PASSWORD_MIN_LENGTH, isPasswordPolicySatisfied } from './auth-validators';

/**
 * Yêu cầu mật khẩu hiện theo lúc gõ (canvas A-02 / A-03): `checklist` liệt kê từng điều kiện kèm dấu ✓,
 * `summary` gộp một dòng và đổi thành "Đạt yêu cầu" khi đủ điều kiện.
 */
@Component({
  selector: 'app-password-rules',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (mode() === 'summary') {
      <p class="rule" [class.ok]="satisfied()" aria-live="polite">
        {{ satisfied() ? '✓ Đạt yêu cầu: ít nhất' : 'Ít nhất' }} 8 ký tự, có chữ và số
      </p>
    } @else {
      <ul aria-live="polite">
        <li class="rule" [class.ok]="longEnough()">{{ longEnough() ? '✓' : '○' }} Ít nhất {{ minLength }} ký tự</li>
        <li class="rule" [class.ok]="hasLetterAndDigit()">{{ hasLetterAndDigit() ? '✓' : '○' }} Có chữ và số</li>
      </ul>
    }
  `,
  styles: `
    ul {
      margin: 6px 0 0;
      padding: 0;
      list-style: none;
    }
    .rule {
      margin: 6px 0 0;
      color: var(--cf-text-muted);
      font-size: 13px;
    }
    ul .rule {
      margin: 2px 0 0;
    }
    .rule.ok {
      color: var(--cf-income);
    }
  `,
})
export class PasswordRules {
  readonly control = input.required<FormControl<string>>();
  readonly mode = input<'checklist' | 'summary'>('checklist');

  protected readonly minLength = PASSWORD_MIN_LENGTH;
  private readonly value = toSignal(
    toObservable(this.control).pipe(
      switchMap((control) => control.valueChanges.pipe(startWith(control.value))),
      map((value) => value ?? ''),
    ),
    { initialValue: '' },
  );

  protected readonly longEnough = computed(() => this.value().length >= PASSWORD_MIN_LENGTH);
  protected readonly hasLetterAndDigit = computed(() => /\p{L}/u.test(this.value()) && /\p{N}/u.test(this.value()));
  protected readonly satisfied = computed(() => isPasswordPolicySatisfied(this.value()));
}
