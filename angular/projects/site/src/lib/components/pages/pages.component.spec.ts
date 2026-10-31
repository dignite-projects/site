import { PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { eSitePolicyNames } from '../../enums/policy-names';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import { SiteTemplateAdminService } from '../../proxy/dignite/site/admin/templates/site-template-admin.service';
import type { PageDto } from '../../proxy/dignite/site/pages/models';
import type { SiteTemplateDto } from '../../proxy/dignite/site/templates/models';
import { SiteTemplateKind } from '../../proxy/dignite/site/templates/site-template-kind.enum';
import { PagesComponent } from './pages.component';

describe('PagesComponent', () => {
  const page = (name: string) =>
    ({ id: name, name, displayName: `Display ${name}`, template: 'Default' }) as PageDto;

  const template = (name: string, kind = SiteTemplateKind.Page, publishedVersionNumber: number | null = 1) =>
    ({ id: name, name, kind, publishedVersionNumber, latestVersionNumber: 1 }) as SiteTemplateDto;

  function create(
    overrides: {
      confirmation?: object;
      toaster?: object;
      pageService?: object;
      templateService?: object;
      canCreateTemplates?: boolean;
    } = {},
  ) {
    const canCreateTemplates = overrides.canCreateTemplates ?? true;

    TestBed.configureTestingModule({
      providers: [
        PagesComponent,
        {
          provide: PageAdminService,
          useValue: { getList: vi.fn(() => of({ items: [] })), ...overrides.pageService },
        },
        {
          provide: SiteTemplateAdminService,
          useValue: { getList: vi.fn(() => of({ items: [] })), ...overrides.templateService },
        },
        { provide: ConfirmationService, useValue: overrides.confirmation ?? {} },
        { provide: ToasterService, useValue: overrides.toaster ?? {} },
        { provide: Router, useValue: {} },
        {
          provide: PermissionService,
          useValue: {
            getGrantedPolicy: (policy: string) =>
              policy === eSitePolicyNames.TemplatesCreate ? canCreateTemplates : true,
          },
        },
      ],
    });

    return TestBed.inject(PagesComponent);
  }

  describe('delete', () => {
    it('asks with the message that says its content types and contents go too, and names the page', () => {
      const warn = vi.fn(() => of(Confirmation.Status.dismiss));
      const remove = vi.fn(() => of(undefined));
      const component = create({ confirmation: { warn }, pageService: { delete: remove } });

      component.delete(page('blog'));

      expect(warn).toHaveBeenCalledWith('Site::DeletePageConfirmMessage', 'AbpUi::AreYouSure', {
        messageLocalizationParams: ['Display blog'],
      });
      expect(remove).not.toHaveBeenCalled();
    });

    it('deletes the page and reloads the list once the user confirms', () => {
      const warn = vi.fn(() => of(Confirmation.Status.confirm));
      const remove = vi.fn(() => of(undefined));
      const success = vi.fn();
      const getList = vi.fn(() => of({ items: [] }));
      const component = create({
        confirmation: { warn },
        toaster: { success },
        pageService: { delete: remove, getList },
      });
      getList.mockClear();

      component.delete(page('blog'));

      expect(remove).toHaveBeenCalledWith('blog');
      expect(getList).toHaveBeenCalledTimes(1);
      expect(success).toHaveBeenCalledWith('AbpUi::SuccessfullyDeleted');
    });
  });

  describe('template options', () => {
    it('loads the Scriban page templates when the modal opens for a new page', () => {
      const getList = vi.fn(() =>
        of({
          items: [
            template('Default'),
            template('layout', SiteTemplateKind.Layout),
            template('partials/card', SiteTemplateKind.Partial),
            template('assets/site.css', SiteTemplateKind.Asset),
            template('Blog/Post', SiteTemplateKind.Page, null),
          ],
        }),
      );
      const component = create({ templateService: { getList } });

      expect(getList).not.toHaveBeenCalled();

      component.openCreate();

      expect(getList).toHaveBeenCalledWith({}, { skipHandleError: true });
      expect(component.templateOptions().map(option => option.name)).toEqual(['Default', 'Blog/Post']);
    });

    it('loads them again each time an existing page is opened', () => {
      const getList = vi.fn(() => of({ items: [template('Default')] }));
      const component = create({ templateService: { getList } });

      component.openEdit(page('blog'));
      component.closeModal();
      component.openEdit(page('news'));

      expect(getList).toHaveBeenCalledTimes(2);
    });

    it('leaves the list empty, silently, when the templates cannot be listed', () => {
      const getList = vi.fn(() => throwError(() => new Error('forbidden')));
      const component = create({ templateService: { getList } });

      component.openEdit(page('blog'));

      expect(component.templateOptions()).toEqual([]);
      expect(component.isModalOpen()).toBe(true);
    });
  });

  describe('showCreateTemplateLink', () => {
    const withTemplates = () => ({ getList: vi.fn(() => of({ items: [template('Default')] })) });

    it('shows for an existing page whose template is not a listed Scriban template', () => {
      const component = create({ templateService: withTemplates() });

      component.openEdit(page('blog'));
      component.form!.patchValue({ template: 'Blog/Post' });

      expect(component.showCreateTemplateLink()).toBe(true);
    });

    it('hides once the typed name is a listed template, with or without a leading slash', () => {
      const component = create({ templateService: withTemplates() });

      component.openEdit(page('blog'));
      component.form!.patchValue({ template: 'Default' });
      expect(component.showCreateTemplateLink()).toBe(false);

      component.form!.patchValue({ template: '/Default' });
      expect(component.showCreateTemplateLink()).toBe(false);
    });

    it('hides when creating a page', () => {
      const component = create({ templateService: withTemplates() });

      component.openCreate();
      component.form!.patchValue({ template: 'Blog/Post' });

      expect(component.showCreateTemplateLink()).toBe(false);
    });

    it('hides without the permission to create templates', () => {
      const component = create({ templateService: withTemplates(), canCreateTemplates: false });

      component.openEdit(page('blog'));
      component.form!.patchValue({ template: 'Blog/Post' });

      expect(component.showCreateTemplateLink()).toBe(false);
    });

    it('hides while the template name is blank', () => {
      const component = create({ templateService: withTemplates() });

      component.openEdit(page('blog'));
      component.form!.patchValue({ template: '  ' });

      expect(component.showCreateTemplateLink()).toBe(false);
    });

    it('shows when the templates could not be listed, since no name is known to exist', () => {
      const getList = vi.fn(() => throwError(() => new Error('forbidden')));
      const component = create({ templateService: { getList } });

      component.openEdit(page('blog'));
      component.form!.patchValue({ template: 'Blog/Post' });

      expect(component.showCreateTemplateLink()).toBe(true);
    });
  });
});
