import { CoreModule } from '@abp/ng.core';
import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { FieldTypeConfigBase } from '@dignite/ng.flex-fields';
import { ContentTypeAdminService } from '../../proxy/dignite/site/admin/content-types/content-type-admin.service';
import type { ContentTypeDto } from '../../proxy/dignite/site/content-types/models';
import { ContentConfiguration } from './content-configuration';

/** Designer-side editor for a `Content` field's configuration: which content type to restrict to (if
 * any), single- vs. multi-select, and a placeholder. */
@Component({
  selector: 'site-content-config',
  templateUrl: './content-config.component.html',
  imports: [CoreModule, CommonModule, ReactiveFormsModule],
})
export class ContentConfigComponent extends FieldTypeConfigBase implements OnInit {
  private readonly contentTypeAdminService = inject(ContentTypeAdminService);

  contentTypes: ContentTypeDto[] = [];

  protected configurationDefaults(): object {
    return new ContentConfiguration();
  }

  ngOnInit(): void {
    this.contentTypeAdminService.getList().subscribe(result => {
      this.contentTypes = result.items ?? [];
    });
  }
}
