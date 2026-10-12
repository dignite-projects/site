import { Component, ChangeDetectionStrategy } from '@angular/core';
import { DynamicLayoutComponent } from '@abp/ng.core';
import { LoaderBarComponent } from '@abp/ng.theme.shared';


@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-loader-bar />
    <abp-dynamic-layout />
  `,
  imports: [LoaderBarComponent, DynamicLayoutComponent],
})
export class AppComponent {}
