import { SettingTabsService } from '@abp/ng.setting-management/config';
import { eSitePolicyNames } from '@dignite/ng.site';
import { inject, makeEnvironmentProviders, provideAppInitializer } from '@angular/core';
import { LanguagesSettingComponent } from '../languages-setting.component';

/** The name of the tab: a localization key of the `Site` resource, which the tab strip localizes. */
export const SITE_LANGUAGES_SETTING_TAB_NAME = 'Site::Languages';

export function configureSiteSettingTabs() {
  inject(SettingTabsService).add([
    {
      name: SITE_LANGUAGES_SETTING_TAB_NAME,
      order: 10,
      requiredPolicy: eSitePolicyNames.Languages,
      component: LanguagesSettingComponent,
    },
  ]);
}

/**
 * Adds the site's languages to Setting Management's page as a tab. Next to `provideSettingManagementConfig()` in the
 * application's configuration: the page is that module's, and an application that does not use it does not use this.
 */
export function provideSiteSettingTabs() {
  return makeEnvironmentProviders([provideAppInitializer(configureSiteSettingTabs)]);
}
