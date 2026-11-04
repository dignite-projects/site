import { CoreModule, PermissionDirective } from '@abp/ng.core';
import { ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Dignite, eSitePolicyNames } from '@dignite/ng.site';

/**
 * The languages the site serves, as a tab of Setting Management's page: the whole ordered list: the first is the default, whose pages have no language
 * prefix in their address. The server refuses a tag it does not recognize and canonicalizes the ones it keeps, so the
 * list shown after a save is what it stored.
 */
@Component({
  selector: 'site-languages-setting',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './languages-setting.component.html',
  imports: [CoreModule, ThemeSharedModule, FormsModule, PermissionDirective],
})
export class LanguagesSettingComponent {
  private readonly service = inject(Dignite.Site.Admin.Languages.SiteLanguageAdminService);
  private readonly toaster = inject(ToasterService);

  readonly policies = eSitePolicyNames;

  // Signals, not fields: the layout shell is OnPush, and a value set in a subscribe callback would not repaint.
  readonly languages = signal<string[]>([]);
  readonly saved = signal<string[]>([]);
  readonly newLanguage = signal('');
  readonly saving = signal(false);

  readonly isDirty = computed(() => this.languages().join(',') !== this.saved().join(','));

  constructor() {
    this.load();
  }

  load() {
    this.service.get().subscribe(result => this.show(result.enabledLanguages));
  }

  /** Adds the tag typed, once; the server decides whether it is a language. */
  add() {
    const tag = this.newLanguage().trim();
    if (!tag) {
      return;
    }

    if (!this.languages().some(language => language.toLowerCase() === tag.toLowerCase())) {
      this.languages.update(list => [...list, tag]);
    }
    this.newLanguage.set('');
  }

  /** The last language cannot go: a site serves at least one. */
  remove(index: number) {
    if (this.languages().length <= 1) {
      return;
    }
    this.languages.update(list => list.filter((_, i) => i !== index));
  }

  move(index: number, offset: -1 | 1) {
    const target = index + offset;
    if (target < 0 || target >= this.languages().length) {
      return;
    }
    this.languages.update(list => {
      const next = [...list];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  makeDefault(index: number) {
    if (index <= 0) {
      return;
    }
    this.languages.update(list => [list[index], ...list.filter((_, i) => i !== index)]);
  }

  save() {
    if (this.saving() || !this.isDirty()) {
      return;
    }

    this.saving.set(true);
    this.service.update({ enabledLanguages: this.languages() }).subscribe({
      next: result => {
        this.show(result.enabledLanguages);
        this.saving.set(false);
        this.toaster.success('AbpUi::SavedSuccessfully');
      },
      error: () => this.saving.set(false),
    });
  }

  private show(languages: string[]) {
    this.languages.set([...languages]);
    this.saved.set([...languages]);
  }
}
