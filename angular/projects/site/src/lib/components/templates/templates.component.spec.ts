import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { SiteTemplateAdminService } from '../../proxy/dignite/site/admin/templates/site-template-admin.service';
import type { SiteTemplateDto } from '../../proxy/dignite/site/templates/models';
import { SiteTemplateKind } from '../../proxy/dignite/site/templates/site-template-kind.enum';
import { TemplatesComponent } from './templates.component';

describe('TemplatesComponent', () => {
  const template = (name: string) =>
    ({ id: name, name, latestVersionNumber: 2, publishedVersionNumber: 1 }) as SiteTemplateDto;

  function create(overrides: { confirmation?: object; toaster?: object; templateService?: object } = {}) {
    TestBed.configureTestingModule({
      providers: [
        TemplatesComponent,
        {
          provide: SiteTemplateAdminService,
          useValue: {
            getList: vi.fn(() => of({ items: [] })),
            getUnmatchedPages: vi.fn(() => of({ items: [] })),
            ...overrides.templateService,
          },
        },
        { provide: ConfirmationService, useValue: overrides.confirmation ?? {} },
        { provide: ToasterService, useValue: overrides.toaster ?? {} },
      ],
    });

    return TestBed.inject(TemplatesComponent);
  }

  it('loads the list on init', () => {
    const getList = vi.fn(() => of({ items: [template('layout'), template('Default')] }));
    const component = create({ templateService: { getList } });

    expect(getList).toHaveBeenCalledTimes(1);
    expect(getList).toHaveBeenCalledWith({ filter: undefined });
    expect(component.rows().map(row => row.name)).toEqual(['layout', 'Default']);
    expect(component.isLoading()).toBe(false);
  });

  it('loads the pages that no published template renders next to the list', () => {
    const unmatched = { pageId: 'p1', name: 'about', displayName: 'About', template: 'About', hasDraft: true };
    const getUnmatchedPages = vi.fn(() => of({ items: [unmatched] }));
    const component = create({ templateService: { getUnmatchedPages } });

    expect(getUnmatchedPages).toHaveBeenCalledTimes(1);
    expect(component.unmatchedPages()).toEqual([unmatched]);

    component.load();
    expect(getUnmatchedPages).toHaveBeenCalledTimes(2);
  });

  it('names the badge of a template by its kind', () => {
    const component = create();

    expect(component.kindKey({ kind: SiteTemplateKind.Page } as SiteTemplateDto)).toBe('Site::TemplateKind:Page');
    expect(component.kindKey({ kind: SiteTemplateKind.Layout } as SiteTemplateDto)).toBe('Site::TemplateKind:Layout');
    expect(component.kindKey({ kind: SiteTemplateKind.Partial } as SiteTemplateDto)).toBe('Site::TemplateKind:Partial');
    expect(component.kindKey({ kind: SiteTemplateKind.Asset } as SiteTemplateDto)).toBe('Site::TemplateKind:Asset');
    expect(component.kindKey({} as SiteTemplateDto)).toBe('Site::TemplateKind:Page');
  });

  it('sends the trimmed search text as the filter', () => {
    const getList = vi.fn(() => of({ items: [] }));
    const component = create({ templateService: { getList } });

    component.filter = '  blog ';
    component.load();

    expect(getList).toHaveBeenLastCalledWith({ filter: 'blog' });
  });

  describe('delete', () => {
    it('asks with the message that says every version goes, and names the template', () => {
      const warn = vi.fn(() => of(Confirmation.Status.dismiss));
      const remove = vi.fn(() => of(undefined));
      const component = create({ confirmation: { warn }, templateService: { delete: remove } });

      component.delete(template('Blog/Article'));

      expect(warn).toHaveBeenCalledWith('Site::DeleteTemplateConfirmMessage', 'AbpUi::AreYouSure', {
        messageLocalizationParams: ['Blog/Article'],
      });
      expect(remove).not.toHaveBeenCalled();
    });

    it('deletes the template and reloads the list once the user confirms', () => {
      const warn = vi.fn(() => of(Confirmation.Status.confirm));
      const remove = vi.fn(() => of(undefined));
      const success = vi.fn();
      const getList = vi.fn(() => of({ items: [] }));
      const component = create({
        confirmation: { warn },
        toaster: { success },
        templateService: { delete: remove, getList },
      });
      getList.mockClear();

      component.delete(template('Default'));

      expect(remove).toHaveBeenCalledWith('Default');
      expect(getList).toHaveBeenCalledTimes(1);
      expect(success).toHaveBeenCalledWith('AbpUi::SuccessfullyDeleted');
    });
  });
});
