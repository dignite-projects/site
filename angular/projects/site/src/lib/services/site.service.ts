import { inject, Injectable } from '@angular/core';
import { RestService } from '@abp/ng.core';

@Injectable({
  providedIn: 'root',
})
export class SiteService {
  apiName = 'Site';

  private restService = inject(RestService);
}
