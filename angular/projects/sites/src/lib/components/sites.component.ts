import { Component, inject } from '@angular/core';
import { SitesService } from '../services/sites.service';

@Component({
  selector: 'lib-sites',
  template: ` <p>sites works!</p> `,
})
export class SitesComponent {
  protected readonly service = inject(SitesService);
}
