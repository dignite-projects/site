import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { DefaultQueueManager, QUEUE_MANAGER } from '@abp/ng.core';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { Subject, of } from 'rxjs';
import { ContentTypeAdminService } from '../../proxy/dignite/site/admin/content-types/content-type-admin.service';
import { PageAdminService } from '../../proxy/dignite/site/admin/pages/page-admin.service';
import type { ContentTypeDto } from '../../proxy/dignite/site/content-types/models';
import { SiteReferenceDataService } from '../../services/site-reference-data.service';
import { ContentTypesComponent } from './content-types.component';

/**
 * The host layout puts the routed component under OnPush ancestors (`abp-router-outlet`, the LeptonX
 * layouts). A Default/Eager component below one is only refreshed when something marks it for check -
 * a plain-field write in an HTTP callback does not, so the list stayed empty until an unrelated event
 * (e.g. clicking Search) did. State held in signals marks its ancestors itself.
 */
@Component({
  selector: 'site-test-on-push-ancestor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ContentTypesComponent],
  template: '<site-content-types />',
})
class OnPushAncestorComponent {}

@Component({
  selector: 'site-test-root',
  imports: [OnPushAncestorComponent],
  template: '<site-test-on-push-ancestor />',
})
class RootComponent {}

/** The pre-fix shape: async data in a plain field, under the same kind of ancestor. */
@Component({
  selector: 'site-test-plain-field',
  changeDetection: ChangeDetectionStrategy.Eager,
  template: '{{ label }}',
})
class PlainFieldComponent {
  label = 'empty';
}

@Component({
  selector: 'site-test-plain-field-ancestor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PlainFieldComponent],
  template: '<site-test-plain-field />',
})
class PlainFieldAncestorComponent {}

@Component({
  selector: 'site-test-plain-field-root',
  imports: [PlainFieldAncestorComponent],
  template: '<site-test-plain-field-ancestor />',
})
class PlainFieldRootComponent {}

describe('ContentTypesComponent under an OnPush ancestor', () => {
  const contentTypes$ = new Subject<{ items: ContentTypeDto[] }>();

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig()],
      providers: [
        { provide: QUEUE_MANAGER, useClass: DefaultQueueManager },
        { provide: ContentTypeAdminService, useValue: { getListByPage: () => contentTypes$ } },
        { provide: PageAdminService, useValue: { get: () => of({ displayName: 'Home' }) } },
        {
          provide: SiteReferenceDataService,
          useValue: {
            getFieldsById: () => of(new Map()),
            getIndexableByFieldType: () => of(new Map()),
            invalidateContentTypes: () => {},
          },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ pageId: 'page-1' }) } },
        },
        { provide: ConfirmationService, useValue: {} },
        { provide: ToasterService, useValue: {} },
      ],
    });
  });

  it('renders the list once the response arrives, with no event to mark the ancestors', () => {
    const fixture = TestBed.createComponent(RootComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Article');

    contentTypes$.next({
      items: [{ id: 'content-type-1', name: 'article', displayName: 'Article', fields: [] } as ContentTypeDto],
    });
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Article');
  });

  it('control: a plain-field write under the same kind of ancestor is never rendered', () => {
    const fixture = TestBed.createComponent(PlainFieldRootComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('empty');

    fixture.debugElement.query(element => element.componentInstance instanceof PlainFieldComponent)
      .componentInstance.label = 'loaded';
    fixture.detectChanges();

    // If this starts failing, Angular stopped skipping Default children below a clean OnPush ancestor,
    // and the signal conversion in ContentTypes/Pages/Fields/Contents is no longer what keeps them live.
    expect(fixture.nativeElement.textContent).toContain('empty');
  });
});
