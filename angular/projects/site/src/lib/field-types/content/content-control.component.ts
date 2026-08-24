import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { AbstractControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { FieldTypeControlBase, readStringList } from '@dignite/ng.flex-fields';
import { ContentAdminService } from '../../proxy/dignite/site/admin/contents/content-admin.service';
import type { ContentDto } from '../../proxy/dignite/site/contents/models';
import { ContentConfiguration } from './content-configuration';

/**
 * Edits the value of a `Content` field: a picker over other Content, restricted to
 * `Content.ContentTypeId` when set. The value is always a flat array of ids, even in single-select mode
 * - matching the server's `ContentFieldType`, which always stores `List<Guid>`.
 *
 * The picker list is fetched once, in {@link ngOnInit} - `Content.ContentTypeId` is fixed for the
 * lifetime of one rendered control (it belongs to the field's own definition, not something this
 * component's own inputs can change), so there is nothing to react to afterward.
 */
@Component({
  selector: 'site-content-control',
  templateUrl: './content-control.component.html',
  imports: [CommonModule, ReactiveFormsModule],
})
export class ContentControlComponent extends FieldTypeControlBase implements OnInit {
  private readonly contentAdminService = inject(ContentAdminService);

  options: ContentDto[] = [];

  get multiple(): boolean {
    return !!this.fieldValue?.field.configuration['Content.Multiple'];
  }

  get placeholder(): string {
    return (this.fieldValue?.field.configuration['Content.Placeholder'] as string) ?? '';
  }

  protected configurationDefaults(): object {
    return new ContentConfiguration();
  }

  protected createControl(): AbstractControl {
    const validators: ValidatorFn[] = [];
    if (this.fieldValue!.required) {
      validators.push(Validators.required);
    }

    const stored = readStringList(this.selectedValue).filter(value => value !== '');
    return this.fb.control(this.multiple ? stored : (stored[0] ?? ''), validators);
  }

  ngOnInit(): void {
    const contentTypeId = this.fieldValue?.field.configuration['Content.ContentTypeId'] as
      | string
      | null
      | undefined;

    this.contentAdminService
      .getList({ contentTypeId: contentTypeId || null, skipCount: 0, maxResultCount: 100 })
      .subscribe(result => {
        this.options = result.items ?? [];
      });
  }
}
