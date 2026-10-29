import { TestBed } from '@angular/core/testing';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { FieldTypeResolver } from '@dignite/ng.flex-fields';
import { of } from 'rxjs';
import { FieldAdminService } from '../../proxy/dignite/site/admin/fields/field-admin.service';
import type { FieldDto } from '../../proxy/dignite/site/fields/models';
import { SiteReferenceDataService } from '../../services/site-reference-data.service';
import { FieldsComponent } from './fields.component';

describe('FieldsComponent', () => {
  const field = (name: string, groupName?: string | null) =>
    ({ id: name, name, displayName: name, groupName }) as FieldDto;

  function create(
    items: FieldDto[],
    overrides: { confirmation?: object; toaster?: object; fieldService?: object } = {},
  ) {
    TestBed.configureTestingModule({
      providers: [
        FieldsComponent,
        {
          provide: FieldAdminService,
          useValue: { getList: vi.fn(() => of({ items })), ...overrides.fieldService },
        },
        {
          provide: SiteReferenceDataService,
          useValue: { getFieldsById: vi.fn(() => of(new Map())), invalidateFields: vi.fn() },
        },
        { provide: FieldTypeResolver, useValue: { getAll: () => [] } },
        { provide: ConfirmationService, useValue: overrides.confirmation ?? {} },
        { provide: ToasterService, useValue: overrides.toaster ?? {} },
      ],
    });

    return TestBed.inject(FieldsComponent);
  }

  it('loads the library as soon as it is constructed', () => {
    const component = create([field('a'), field('b')]);

    expect(component.data().items).toHaveLength(2);
  });

  it('buckets fields by group, named groups alphabetical, ungrouped last', () => {
    const component = create([
      field('loose'),
      field('z-field', 'Zeta'),
      field('a-field', 'Alpha'),
      field('a-field-2', 'Alpha'),
    ]);

    expect(component.sections().map(section => [section.groupName, section.fields.map(f => f.name)])).toEqual([
      ['Alpha', ['a-field', 'a-field-2']],
      ['Zeta', ['z-field']],
      [null, ['loose']],
    ]);
  });

  it('files a blank or whitespace-only group name under Ungrouped', () => {
    const component = create([field('a', ''), field('b', '   '), field('c', null)]);

    expect(component.sections()).toHaveLength(1);
    expect(component.sections()[0].groupName).toBeNull();
    expect(component.sections()[0].fields).toHaveLength(3);
  });

  it('has no sections for an empty library', () => {
    expect(create([]).sections()).toEqual([]);
  });

  describe('delete', () => {
    it('asks with the message that says its value goes from every content, and names the field', () => {
      const warn = vi.fn(() => of(Confirmation.Status.dismiss));
      const remove = vi.fn(() => of(undefined));
      const component = create([field('title')], { confirmation: { warn }, fieldService: { delete: remove } });

      component.delete(field('title'));

      expect(warn).toHaveBeenCalledWith('Site::DeleteFieldConfirmMessage', 'AbpUi::AreYouSure', {
        messageLocalizationParams: ['title'],
      });
      expect(remove).not.toHaveBeenCalled();
    });

    it('deletes the field once the user confirms', () => {
      const warn = vi.fn(() => of(Confirmation.Status.confirm));
      const remove = vi.fn(() => of(undefined));
      const success = vi.fn();
      const component = create([field('title')], {
        confirmation: { warn },
        toaster: { success },
        fieldService: { delete: remove },
      });

      component.delete(field('title'));

      expect(remove).toHaveBeenCalledWith('title');
      expect(success).toHaveBeenCalledWith('AbpUi::SuccessfullyDeleted');
    });
  });
});
