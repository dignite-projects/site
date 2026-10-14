import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DefaultQueueManager, QUEUE_MANAGER } from '@abp/ng.core';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { ToasterService } from '@abp/ng.theme.shared';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { Subject, of } from 'rxjs';
import { ContentAdminService } from '../../proxy/dignite/site/admin/contents/content-admin.service';
import { ContentTypeAdminService } from '../../proxy/dignite/site/admin/content-types/content-type-admin.service';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import { SiteReferenceDataService } from '../../services/site-reference-data.service';
import { ContentEditorComponent } from './content-editor.component';

/**
 * The host layout puts the routed component under OnPush ancestors (`abp-router-outlet`, the LeptonX
 * layouts), which a plain-field write in an HTTP callback never marks for check - the editor would stay
 * on its blank loading state until an unrelated event did. See `content-types.component.spec.ts`.
 */
@Component({
  selector: 'site-test-on-push-ancestor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ContentEditorComponent],
  template: '<site-content-editor />',
})
class OnPushAncestorComponent {}

@Component({
  selector: 'site-test-root',
  imports: [OnPushAncestorComponent],
  template: '<site-test-on-push-ancestor />',
})
class RootComponent {}

describe('ContentEditorComponent under an OnPush ancestor', () => {
  const schema$ = new Subject<{ enabledLanguages: string[]; defaultLanguage: string }>();

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
      providers: [
        provideRouter([]),
        { provide: QUEUE_MANAGER, useClass: DefaultQueueManager },
        { provide: ContentAdminService, useValue: {} },
        { provide: ContentTypeAdminService, useValue: {} },
        { provide: PageAdminService, useValue: {} },
        {
          provide: SiteReferenceDataService,
          useValue: { getSchema: () => schema$, getFieldsById: () => of(new Map()) },
        },
        { provide: ToasterService, useValue: {} },
      ],
    });
  });

  it('shows the form once the data arrives, with no event to mark the ancestors', () => {
    const fixture = TestBed.createComponent(RootComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('form')).toBeNull();

    schema$.next({ enabledLanguages: ['en', 'ja'], defaultLanguage: 'en' });
    schema$.complete();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('form')).not.toBeNull();
    expect(
      [...fixture.nativeElement.querySelectorAll('select[formcontrolname="cultureName"] option')].map(
        (option: HTMLOptionElement) => option.value,
      ),
    ).toEqual(['en', 'ja']);
  });
});
