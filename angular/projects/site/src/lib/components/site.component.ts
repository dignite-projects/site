import { Component, inject } from '@angular/core';
import { SiteService } from '../services/site.service';

@Component({
  selector: 'lib-site',
  template: ` <p>site works!</p> `,
})
export class SiteComponent {
  protected readonly service = inject(SiteService);
}
