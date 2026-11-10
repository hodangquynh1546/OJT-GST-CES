import { Component } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { KENDO_BUTTON } from '@progress/kendo-angular-buttons';
import { KENDO_FORMFIELD, KENDO_TEXTBOX } from '@progress/kendo-angular-inputs';
import { KENDO_LABEL } from '@progress/kendo-angular-label';

/** Dựng component Kendo thật (không tự gắn class) để kiểm tra styles.scss áp đúng design-system.md mục 6. */
@Component({
  imports: [ReactiveFormsModule, KENDO_BUTTON, KENDO_TEXTBOX, KENDO_LABEL, KENDO_FORMFIELD],
  template: `
    <kendo-formfield>
      <kendo-label [for]="box" text="Họ tên" />
      <kendo-textbox #box [formControl]="name" />
      <kendo-formhint>Gợi ý</kendo-formhint>
    </kendo-formfield>
    <kendo-textbox class="small" size="small" />
    <button kendoButton themeColor="primary" class="primary">Lưu</button>
    <button kendoButton class="base">Hủy</button>
    <button kendoButton size="small" class="small-button">Nhỏ</button>
  `,
})
class KendoSample {
  readonly name = new FormControl('');
}

describe('Kích thước Kendo toàn app (styles.scss)', () => {
  let root: HTMLElement;

  beforeEach(() => {
    const fixture = TestBed.createComponent(KendoSample);
    fixture.detectChanges();
    root = fixture.nativeElement as HTMLElement;
    document.body.appendChild(root);
  });

  afterEach(() => root.remove());

  const el = (selector: string) => root.querySelector(selector) as HTMLElement;
  const style = (selector: string) => getComputedStyle(el(selector));

  it('ô nhập medium 40 px, small 32 px, viền --cf-border-strong', () => {
    expect(el('kendo-textbox:not(.small)').offsetHeight).toBe(40);
    expect(el('kendo-textbox.small').offsetHeight).toBe(32);
    expect(style('kendo-textbox:not(.small)').borderTopColor).toBe('rgb(196, 204, 212)');
  });

  it('nút medium 40 px; nút chính đậm 600; nút phụ nền trắng có viền; nút small 32 px', () => {
    expect(el('button.primary').offsetHeight).toBe(40);
    expect(style('button.primary').fontWeight).toBe('600');
    expect(el('button.base').offsetHeight).toBe(40);
    expect(style('button.base').backgroundColor).toBe('rgb(255, 255, 255)');
    expect(style('button.base').borderTopColor).toBe('rgb(196, 204, 212)');
    expect(el('button.small-button').offsetHeight).toBe(32);
  });

  it('nhãn 13 px đậm 600; gợi ý 12 px chữ thẳng màu phụ', () => {
    expect(style('label.k-label').fontSize).toBe('13px');
    expect(style('label.k-label').fontWeight).toBe('600');
    expect(style('kendo-formhint').fontStyle).toBe('normal');
    expect(style('kendo-formhint').fontSize).toBe('12px');
    expect(style('kendo-formhint').color).toBe('rgb(89, 99, 110)');
  });
});
