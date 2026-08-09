import { CoreModule, ListResultDto, PermissionDirective } from '@abp/ng.core';
import {
  Confirmation,
  ConfirmationService,
  ThemeSharedModule,
  ToasterService,
} from '@abp/ng.theme.shared';
import { Component, ElementRef, ViewChild, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzAutocompleteModule } from 'ng-zorro-antd/auto-complete';
import { Observable, forkJoin, map, switchMap } from 'rxjs';
import {
  FieldTypeDefinition,
  FieldTypeResolver,
  FlexFieldConfigComponent,
} from '@dignite/ng.flex-fields';
import type { FlexFieldData } from '@dignite/ng.flex-fields';
import { FieldAdminService } from '../../proxy/dignite/site/admin/fields/field-admin.service';
import type { GetFieldListInput } from '../../proxy/dignite/site/admin/fields/models';
import type { FieldDto } from '../../proxy/dignite/site/fields/models';
import { eSitePolicyNames } from '../../enums/policy-names';
import { SiteReferenceDataService } from '../../services/site-reference-data.service';

const MAX_FIELD_NAME_LENGTH = 64;
const MAX_FIELD_DISPLAY_NAME_LENGTH = 128;
const MAX_FIELD_DESCRIPTION_LENGTH = 256;
const MAX_GROUP_NAME_LENGTH = 64;

/**
 * localStorage key for per-group collapse state the user has explicitly chosen, keyed by group name
 * (`''` for Ungrouped - a real group name is never empty after {@link FieldsComponent.rebuildSections}
 * trims it). Groups absent here fall back to the built-in default.
 */
const GROUP_COLLAPSE_STATE_STORAGE_KEY = 'Dignite.Site.Fields.GroupCollapseState';

/** One group heading plus the fields filed under it. */
interface FieldGroupSection {
  /** Null for the ungrouped fields, which sort last. */
  groupName: string | null;
  fields: FieldDto[];
}

/**
 * The tenant's field library, listed in one column with a heading per group.
 *
 * **Grouping is a label on the field, not an entity.** `Field.GroupName` is a plain string, so an
 * editor files a field under a new group by typing it - there is no "create the group first" step, and
 * a group stops existing when the last field leaves it.
 *
 * **No Required/Searchable here.** Unlike the flex-fields demo, whose `ProductFieldDto` carries those
 * flags on the definition, Site keeps them per *usage* on `ContentTypeFieldDto` - the same field can be
 * pulled into several content types with different settings (总体设计 §2.3). They are set in the
 * content type's field arrangement.
 */
@Component({
  selector: 'site-fields',
  templateUrl: './fields.component.html',
  imports: [
    CoreModule,
    ThemeSharedModule,
    ReactiveFormsModule,
    PermissionDirective,
    FlexFieldConfigComponent,
    NzAutocompleteModule,
  ],
})
export class FieldsComponent {
  private readonly fieldService = inject(FieldAdminService);
  private readonly referenceData = inject(SiteReferenceDataService);
  private readonly fieldTypeResolver = inject(FieldTypeResolver);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);

  readonly policies = eSitePolicyNames;
  readonly fieldTypes: readonly FieldTypeDefinition[] = this.fieldTypeResolver.getAll();

  /** The whole field library, unpaged - grouped display has no notion of "current page". */
  data: ListResultDto<FieldDto> = { items: [] };
  filters = {} as GetFieldListInput;

  /** The whole field library, bucketed by group for rendering. */
  sections: FieldGroupSection[] = [];

  /** Every group name in use, for the editor's suggestion list. */
  knownGroupNames: string[] = [];

  /**
   * Per-group collapse state the user has explicitly toggled, keyed by group name (`''` for
   * Ungrouped). Client-side only, persisted in localStorage. A group absent here uses the built-in
   * default: Ungrouped starts open (it is the catch-all, worth seeing at a glance), named groups
   * start closed.
   */
  private readonly groupCollapseOverrides = new Map<string, boolean>(this.loadGroupCollapseOverrides());

  /**
   * Backs the group `nz-autocomplete`. Filters `knownGroupNames` client-side against the form's current
   * text rather than a server round trip - the list is already loaded, and there is no separate id: a
   * group's key IS its name.
   */
  get filteredGroupNames(): string[] {
    const term = ((this.form?.get('groupName')?.value as string) ?? '').trim().toLowerCase();
    return term ? this.knownGroupNames.filter(name => name.toLowerCase().includes(term)) : this.knownGroupNames;
  }

  isModalOpen = false;
  isModalBusy = false;
  editingField?: FieldDto;
  form?: FormGroup;
  selectedFieldData?: FlexFieldData;

  isRenameModalOpen = false;
  isRenameModalBusy = false;
  renamingField?: FieldDto;
  renameForm?: FormGroup;

  isGroupModalOpen = false;
  isGroupModalBusy = false;
  renamingGroupName?: string;
  groupForm?: FormGroup;

  @ViewChild('submitButton') submitButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('renameSubmitButton') renameSubmitButton?: ElementRef<HTMLButtonElement>;
  @ViewChild('groupSubmitButton') groupSubmitButton?: ElementRef<HTMLButtonElement>;

  constructor() {
    this.search();
    this.loadKnownGroupNames();
  }

  /** Reloads the whole (unpaged) field library and re-buckets it by group. */
  search(): void {
    this.fieldService.getList({ filter: this.filters.filter }).subscribe(result => {
      this.data = result;
      this.rebuildSections();
    });
  }

  /**
   * Buckets the page's fields by group. Ungrouped goes last: it is the catch-all, and leading with it
   * would bury the named groups under whatever has not been filed yet.
   */
  private rebuildSections(): void {
    const buckets = new Map<string, FieldDto[]>();
    const ungrouped: FieldDto[] = [];

    for (const field of this.data.items ?? []) {
      const groupName = field.groupName?.trim();
      if (!groupName) {
        ungrouped.push(field);
        continue;
      }
      const bucket = buckets.get(groupName);
      if (bucket) {
        bucket.push(field);
      } else {
        buckets.set(groupName, [field]);
      }
    }

    this.sections = [...buckets.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([groupName, fields]) => ({ groupName, fields }));

    if (ungrouped.length > 0) {
      this.sections.push({ groupName: null, fields: ungrouped });
    }
  }

  toggleGroup(groupName: string | null): void {
    this.groupCollapseOverrides.set(groupName ?? '', !this.isGroupCollapsed(groupName));
    this.saveGroupCollapseOverrides();
  }

  isGroupCollapsed(groupName: string | null): boolean {
    return this.groupCollapseOverrides.get(groupName ?? '') ?? groupName !== null;
  }

  private loadGroupCollapseOverrides(): [string, boolean][] {
    try {
      const raw = localStorage.getItem(GROUP_COLLAPSE_STATE_STORAGE_KEY);
      return raw ? Object.entries(JSON.parse(raw) as Record<string, boolean>) : [];
    } catch {
      return [];
    }
  }

  private saveGroupCollapseOverrides(): void {
    localStorage.setItem(
      GROUP_COLLAPSE_STATE_STORAGE_KEY,
      JSON.stringify(Object.fromEntries(this.groupCollapseOverrides)),
    );
  }

  /**
   * Drawn from the shared reference cache rather than `data.items` directly, so it stays in sync with
   * whatever else on the page (or another component) last invalidated it.
   */
  private loadKnownGroupNames(): void {
    this.referenceData.getFieldsById().subscribe(fields => {
      this.knownGroupNames = [
        ...new Set(
          [...fields.values()]
            .map(field => field.groupName?.trim())
            .filter((name): name is string => !!name),
        ),
      ].sort((a, b) => a.localeCompare(b));
    });
  }

  private refreshLibrary(): void {
    this.referenceData.invalidateFields();
    this.loadKnownGroupNames();
    this.search();
  }

  /** Localization key for a field type's display name, for use with `| abpLocalization`. */
  displayNameKeyOf(fieldTypeName?: string): string {
    return (
      this.fieldTypes.find(type => type.name === fieldTypeName)?.displayNameKey ??
      fieldTypeName ??
      ''
    );
  }

  openCreate(): void {
    this.editingField = undefined;
    this.form = this.buildForm();
    this.isModalOpen = true;
  }

  openEdit(field: FieldDto): void {
    this.editingField = field;
    this.form = this.buildForm(field);
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.form = undefined;
    this.editingField = undefined;
  }

  save(): void {
    if (!this.form || this.form.invalid || this.isModalBusy) {
      return;
    }

    this.isModalBusy = true;
    const value = this.form.getRawValue();
    const configuration = this.form.get('configuration')?.value ?? {};
    const groupName = (value.groupName as string)?.trim() || null;

    const request$ = this.editingField
      ? this.fieldService.update(this.editingField.id!, {
          displayName: value.displayName,
          fieldTypeName: value.fieldTypeName,
          description: value.description,
          configuration,
          groupName,
        })
      : this.fieldService.create({
          name: value.name,
          displayName: value.displayName,
          fieldTypeName: value.fieldTypeName,
          description: value.description,
          configuration,
          groupName,
        });

    request$.subscribe({
      next: () => {
        this.isModalBusy = false;
        this.closeModal();
        this.refreshLibrary();
        this.toaster.success('AbpUi::SavedSuccessfully');
      },
      error: () => (this.isModalBusy = false),
    });
  }

  delete(field: FieldDto): void {
    this.confirmation
      .warn('AbpUi::ItemWillBeDeletedMessage', 'AbpUi::AreYouSure', {
        messageLocalizationParams: [field.displayName ?? ''],
      })
      .subscribe((status: Confirmation.Status) => {
        if (status === Confirmation.Status.confirm) {
          // A platform preset answers Site:030002 here; the error handler surfaces the server's own
          // message, which already explains to remove it from an arrangement instead.
          this.fieldService.delete(field.id!).subscribe(() => {
            this.refreshLibrary();
            this.toaster.success('AbpUi::SuccessfullyDeleted');
          });
        }
      });
  }

  /**
   * Renaming is deliberately its own operation, not part of the edit form: `UpdateFieldDto` carries no
   * name, the server guards it with its own permission (`Admin.Fields.Rename`), and it rewrites the
   * stored value of every content using the field.
   */
  openRename(field: FieldDto): void {
    this.renamingField = field;
    this.renameForm = this.fb.group({
      newName: [
        field.name ?? '',
        [Validators.required, Validators.maxLength(MAX_FIELD_NAME_LENGTH)],
      ],
    });
    this.isRenameModalOpen = true;
  }

  closeRenameModal(): void {
    this.isRenameModalOpen = false;
    this.renameForm = undefined;
    this.renamingField = undefined;
  }

  saveRename(): void {
    if (!this.renameForm || this.renameForm.invalid || this.isRenameModalBusy) {
      return;
    }

    this.isRenameModalBusy = true;

    this.fieldService.rename(this.renamingField!.id!, this.renameForm.getRawValue()).subscribe({
      next: () => {
        this.isRenameModalBusy = false;
        this.closeRenameModal();
        this.refreshLibrary();
        this.toaster.success('AbpUi::SavedSuccessfully');
      },
      error: () => (this.isRenameModalBusy = false),
    });
  }

  /**
   * Group is a label, not an entity ({@link FieldGroupSection}), so "editing" one is a bulk rename of
   * `groupName` across every field that carries it - scoped to the whole library via a fresh fetch in
   * `fieldsInGroup`, not just `section.fields` (which reflects whatever the search box currently
   * filters `data.items` to).
   */
  openEditGroup(groupName: string): void {
    this.renamingGroupName = groupName;
    this.groupForm = this.fb.group({
      groupName: [groupName, [Validators.required, Validators.maxLength(MAX_GROUP_NAME_LENGTH)]],
    });
    this.isGroupModalOpen = true;
  }

  closeGroupModal(): void {
    this.isGroupModalOpen = false;
    this.groupForm = undefined;
    this.renamingGroupName = undefined;
  }

  saveGroupRename(): void {
    if (!this.groupForm || this.groupForm.invalid || this.isGroupModalBusy || !this.renamingGroupName) {
      return;
    }

    const oldGroupName = this.renamingGroupName;
    const newGroupName = (this.groupForm.getRawValue().groupName as string).trim();

    if (newGroupName === oldGroupName) {
      this.closeGroupModal();
      return;
    }

    this.isGroupModalBusy = true;
    this.fieldsInGroup(oldGroupName)
      .pipe(
        switchMap(fields => forkJoin(fields.map(field => this.updateFieldGroupName(field, newGroupName)))),
      )
      .subscribe({
        next: () => {
          this.isGroupModalBusy = false;
          this.closeGroupModal();
          this.refreshLibrary();
          this.toaster.success('AbpUi::SavedSuccessfully');
        },
        error: () => (this.isGroupModalBusy = false),
      });
  }

  /** Ungroups every field in the group - the group itself is only ever the fields that carry its name. */
  deleteGroup(groupName: string): void {
    this.fieldsInGroup(groupName).subscribe(fields => {
      this.confirmation
        .warn('Site::UngroupConfirmMessage', 'AbpUi::AreYouSure', {
          messageLocalizationParams: [String(fields.length), groupName],
        })
        .subscribe((status: Confirmation.Status) => {
          if (status === Confirmation.Status.confirm) {
            forkJoin(fields.map(field => this.updateFieldGroupName(field, null))).subscribe(() => {
              this.refreshLibrary();
              this.toaster.success('AbpUi::SuccessfullyDeleted');
            });
          }
        });
    });
  }

  /**
   * The group's full membership, library-wide - a fresh, unfiltered fetch so a rename/ungroup is never
   * scoped down by whatever the search box currently filters `data.items` to.
   */
  private fieldsInGroup(groupName: string): Observable<FieldDto[]> {
    return this.fieldService
      .getList({})
      .pipe(map(result => (result.items ?? []).filter(field => field.groupName === groupName)));
  }

  private updateFieldGroupName(field: FieldDto, groupName: string | null) {
    return this.fieldService.update(field.id!, {
      displayName: field.displayName!,
      fieldTypeName: field.fieldTypeName!,
      description: field.description,
      configuration: field.configuration,
      groupName,
    });
  }

  private buildForm(field?: FieldDto): FormGroup {
    this.selectedFieldData = field
      ? {
          id: field.id!,
          name: field.name!,
          displayName: field.displayName!,
          description: field.description ?? undefined,
          fieldTypeName: field.fieldTypeName!,
          configuration: (field.configuration ?? {}) as Record<string, unknown>,
        }
      : undefined;

    return this.fb.group({
      // Fixed after creation - renaming goes through `openRename`. Kept in the form (disabled) so the
      // modal can show what it is.
      name: [
        { value: field?.name ?? '', disabled: !!field },
        [Validators.required, Validators.maxLength(MAX_FIELD_NAME_LENGTH)],
      ],
      displayName: [
        field?.displayName ?? '',
        [Validators.required, Validators.maxLength(MAX_FIELD_DISPLAY_NAME_LENGTH)],
      ],
      description: [field?.description ?? '', [Validators.maxLength(MAX_FIELD_DESCRIPTION_LENGTH)]],
      // Free text with a suggestion list rather than a closed dropdown: filing a field under a new
      // group is just typing its name.
      groupName: [field?.groupName ?? '', [Validators.maxLength(MAX_GROUP_NAME_LENGTH)]],
      // Also fixed after creation: changing the type would orphan every stored value. Still bound so
      // `ff-flex-field-config` can pick which configuration editor to render.
      fieldTypeName: [
        { value: field?.fieldTypeName ?? this.fieldTypes[0]?.name ?? '', disabled: !!field },
        Validators.required,
      ],
    });
  }
}
