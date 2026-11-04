import { ToasterService } from '@abp/ng.theme.shared';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { Dignite } from '@dignite/ng.site';
import { LanguagesSettingComponent } from '@dignite/ng.site/setting-management';

// Here and not beside the component: the unit-test builder runs the specs under the primary entry's source root.
describe('LanguagesSettingComponent', () => {
  function create(stored = ['zh-Hans', 'en'], overrides: { update?: ReturnType<typeof vi.fn> } = {}) {
    const update =
      overrides.update ??
      vi.fn((input: { enabledLanguages: string[] }) =>
        of({ enabledLanguages: input.enabledLanguages, defaultLanguage: input.enabledLanguages[0] }),
      );
    const success = vi.fn();

    TestBed.configureTestingModule({
      providers: [
        LanguagesSettingComponent,
        {
          provide: Dignite.Site.Admin.Languages.SiteLanguageAdminService,
          useValue: { get: vi.fn(() => of({ enabledLanguages: stored, defaultLanguage: stored[0] })), update },
        },
        { provide: ToasterService, useValue: { success } },
      ],
    });

    return { component: TestBed.inject(LanguagesSettingComponent), update, success };
  }

  it('shows the languages the server has, in order, and nothing to save', () => {
    const { component } = create();

    expect(component.languages()).toEqual(['zh-Hans', 'en']);
    expect(component.isDirty()).toBe(false);
  });

  it('adds a language once, whatever its case, and clears the box', () => {
    const { component } = create();

    component.newLanguage.set(' ja ');
    component.add();
    component.newLanguage.set('JA');
    component.add();

    expect(component.languages()).toEqual(['zh-Hans', 'en', 'ja']);
    expect(component.newLanguage()).toBe('');
    expect(component.isDirty()).toBe(true);
  });

  it('keeps at least one language', () => {
    const { component } = create(['en']);

    component.remove(0);

    expect(component.languages()).toEqual(['en']);
  });

  it('makes a language the default by moving it to the front', () => {
    const { component } = create(['zh-Hans', 'en', 'ja']);

    component.makeDefault(2);

    expect(component.languages()).toEqual(['ja', 'zh-Hans', 'en']);
  });

  it('moves a language by one place and not past either end', () => {
    const { component } = create(['zh-Hans', 'en', 'ja']);

    component.move(0, -1);
    component.move(2, 1);
    component.move(1, 1);

    expect(component.languages()).toEqual(['zh-Hans', 'ja', 'en']);
  });

  it('saves the whole list and shows what the server stored', () => {
    const update = vi.fn(() => of({ enabledLanguages: ['ja', 'en'], defaultLanguage: 'ja' }));
    const { component, success } = create(['en'], { update });

    component.newLanguage.set('ja');
    component.add();
    component.makeDefault(1);
    component.save();

    expect(update).toHaveBeenCalledWith({ enabledLanguages: ['ja', 'en'] });
    expect(component.languages()).toEqual(['ja', 'en']);
    expect(component.isDirty()).toBe(false);
    expect(success).toHaveBeenCalled();
  });

  it('saves nothing when nothing changed', () => {
    const { component, update } = create();

    component.save();

    expect(update).not.toHaveBeenCalled();
  });
});
