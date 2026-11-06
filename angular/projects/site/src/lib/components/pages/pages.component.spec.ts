import { PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import { SiteThemeAdminService } from '../../proxy/dignite/site/admin/themes/site-theme-admin.service';
import type { PageDto } from '../../proxy/dignite/site/pages/models';
import { SiteThemeFileStatus } from '../../proxy/dignite/site/themes/site-theme-file-status.enum';
import { PagesComponent } from './pages.component';

describe('PagesComponent', () => {
  const page = (name: string) => ({ id: name, name, displayName: `Display ${name}` }) as PageDto;

  function create(
    overrides: { confirmation?: object; toaster?: object; pageService?: object; themeService?: object; granted?: boolean } = {},
  ) {
    TestBed.configureTestingModule({
      providers: [
        PagesComponent,
        {
          provide: PageAdminService,
          useValue: { getList: vi.fn(() => of({ items: [] })), ...overrides.pageService },
        },
        {
          provide: SiteThemeAdminService,
          useValue: { getFiles: vi.fn(() => of({ items: [] })), ...overrides.themeService },
        },
        { provide: ConfirmationService, useValue: overrides.confirmation ?? {} },
        { provide: ToasterService, useValue: overrides.toaster ?? {} },
        { provide: Router, useValue: {} },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => overrides.granted ?? true } },
      ],
    });

    return TestBed.inject(PagesComponent);
  }

  describe('template suggestions', () => {
    it("offers the theme's templates, not its static files nor deleted ones", () => {
      const getFiles = vi.fn(() =>
        of({
          items: [
            { path: 'Default', isAsset: false, status: SiteThemeFileStatus.Unchanged },
            { path: 'blog/post', isAsset: false, status: SiteThemeFileStatus.Added },
            { path: 'assets/site.css', isAsset: true, status: SiteThemeFileStatus.Unchanged },
            { path: 'Old', isAsset: false, status: SiteThemeFileStatus.Deleted },
          ],
        }),
      );
      const component = create({ themeService: { getFiles } });

      component.openCreate();

      expect(component.templateOptions()).toEqual(['Default', 'blog/post']);
    });

    it('does not ask for the theme without the Themes permission', () => {
      const getFiles = vi.fn(() => of({ items: [] }));
      const component = create({ themeService: { getFiles }, granted: false });

      component.openCreate();

      expect(getFiles).not.toHaveBeenCalled();
      expect(component.templateOptions()).toEqual([]);
    });
  });

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
      expect(success).toHaveBeenCalledWith('AbpUi::DeletedSuccessfully');
    });
  });
});
