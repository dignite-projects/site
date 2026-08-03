import { inject, Injectable } from '@angular/core';
import { RestService } from '@abp/ng.core';

@Injectable({
  providedIn: 'root',
})
export class SitesService {
  apiName = 'Sites';

  private restService = inject(RestService);
}
