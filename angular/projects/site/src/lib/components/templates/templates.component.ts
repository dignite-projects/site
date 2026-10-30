import { CoreModule, PermissionDirective } from '@abp/ng.core';
import {
  Confirmation,
  ConfirmationService,
  ThemeSharedModule,
  ToasterService,
} from '@abp/ng.theme.shared';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { SiteTemplateAdminService } from '../../proxy/dignite/site/admin/templates/site-template-admin.service';
import type { SiteTemplateDto } from '../../proxy/dignite/site/templates/models';
import { eSitePolicyNames } from '../../enums/policy-names';

@Component({
  selector: 'site-templates',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './templates.component.html',
  imports: [CoreModule, ThemeSharedModule, NgxDatatableModule, PermissionDirective, RouterLink],
})
export class TemplatesComponent {
  private readonly templateService = inject(SiteTemplateAdminService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);

  readonly policies = eSitePolicyNames;

  // State that changes in an HTTP callback is a signal, not a plain field: the host layout's ancestors
  // (`abp-router-outlet`, the LeptonX layouts) are OnPush, and a plain-field write in a response callback
  // never marks them for check, so the list would stay empty until some unrelated event happened to.
  readonly rows = signal<SiteTemplateDto[]>([]);
  readonly isLoading = signal(false);

  /** Bound to the search box; the list is filtered by the server, so it is read when a search is run. */
  filter = '';

  constructor() {
    this.load();
  }

  load(): void {
    this.isLoading.set(true);

    this.templateService.getList({ filter: this.filter.trim() || undefined }).subscribe({
      next: result => {
        this.rows.set(result.items ?? []);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  clearFilter(): void {
    this.filter = '';
    this.load();
  }

  delete(template: SiteTemplateDto): void {
    // Not the generic AbpUi::ItemWillBeDeletedMessage: deleting a template deletes every version of it, and
    // pages that named it fall back to the Razor view of that name - this confirmation is the only place a
    // person is told.
    this.confirmation
      .warn('Site::DeleteTemplateConfirmMessage', 'AbpUi::AreYouSure', {
        messageLocalizationParams: [template.name],
      })
      .subscribe((status: Confirmation.Status) => {
        if (status === Confirmation.Status.confirm) {
          this.templateService.delete(template.id).subscribe(() => {
            this.load();
            this.toaster.success('AbpUi::SuccessfullyDeleted');
          });
        }
      });
  }
}
