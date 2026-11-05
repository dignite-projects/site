import { SettingTabsService } from '@abp/ng.setting-management/config';
import { eSitePolicyNames } from '@dignite/ng.site';
import { inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { LanguagesSettingComponent } from '../languages-setting.component';
import { SeoSettingComponent } from '../seo-setting.component';

/** The name of the tab: a localization key of the `Site` resource, which the tab strip localizes. */
export const SITE_LANGUAGES_SETTING_TAB_NAME = 'Site::Languages';

export const SITE_SEO_SETTING_TAB_NAME = 'Site::Seo';

export function configureSiteSettingTabs() {
  inject(SettingTabsService).add([
    {
      name: SITE_LANGUAGES_SETTING_TAB_NAME,
      order: 10,
      requiredPolicy: eSitePolicyNames.Languages,
      component: LanguagesSettingComponent,
    },
    {
      name: SITE_SEO_SETTING_TAB_NAME,
      order: 20,
      requiredPolicy: eSitePolicyNames.Seo,
      component: SeoSettingComponent,
    },
  ]);
}

/**
 * Adds the site's languages and SEO settings to Setting Management's page as tabs. Next to
 * `provideSettingManagementConfig()` in the application's configuration: the page is that module's, and an
 * application that does not use it does not use this.
 */
export function provideSiteSettingTabs() {
  return makeEnvironmentProviders([provideAppInitializer(configureSiteSettingTabs)]);
}
