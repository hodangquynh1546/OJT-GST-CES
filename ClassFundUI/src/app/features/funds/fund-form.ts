import { AbstractControl, FormControl, FormGroup, ValidationErrors, Validators } from '@angular/forms';

import { FundBody, isValidSchoolYear } from './funds.models';

export const FUND_NAME_MAX = 100;

export function schoolYearValidator(control: AbstractControl<string | null>): ValidationErrors | null {
  return isValidSchoolYear(control.value) ? null : { schoolYear: true };
}

/** Không chấp nhận tên chỉ có khoảng trắng. */
export function notBlankValidator(control: AbstractControl<string | null>): ValidationErrors | null {
  return (control.value ?? '').trim().length > 0 ? null : { required: true };
}

export type FundForm = FormGroup<{
  name: FormControl<string>;
  schoolYear: FormControl<string>;
}>;

export function createFundForm(initial: FundBody): FundForm {
  return new FormGroup({
    name: new FormControl(initial.name, {
      nonNullable: true,
      validators: [notBlankValidator, Validators.maxLength(FUND_NAME_MAX)],
    }),
    schoolYear: new FormControl(initial.schoolYear, { nonNullable: true, validators: [schoolYearValidator] }),
  });
}

export function fundBody(form: FundForm): FundBody {
  const value = form.getRawValue();
  return { name: value.name.trim(), schoolYear: value.schoolYear.trim() };
}
