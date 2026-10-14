import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormBuilder, FormGroup } from '@angular/forms';
import type { FlexFieldValue } from '@dignite/ng.flex-fields';
import { Subject } from 'rxjs';
import { ContentAdminService } from '../../proxy/dignite/site/admin/contents/content-admin.service';
import type { ContentDto } from '../../proxy/dignite/site/contents/models';
import { ContentControlComponent } from './content-control.component';

/**
 * The content editor renders this control in its page flow, under the host layout's OnPush ancestors
 * (`abp-router-outlet`, the LeptonX layouts). A plain-field write in the HTTP callback never marks those
 * for check, so the options (and a stored selection) stayed blank until an unrelated event did. See
 * `content-types.component.spec.ts`.
 */
@Component({
  selector: 'lib-test-on-push-ancestor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ContentControlComponent],
  template: `<site-content-control [fields]="fieldValue" [entity]="form" parentFieldName="flexFields" />`,
})
class OnPushAncestorComponent {
  readonly form: FormGroup = inject(FormBuilder).group({ flexFields: inject(FormBuilder).group({}) });

  readonly fieldValue = {
    field: {
      id: 'field-1',
      name: 'related',
      displayName: 'Related',
      fieldTypeName: 'Content',
      configuration: {},
    },
    required: false,
    searchable: false,
    value: undefined,
  } as FlexFieldValue;
}

@Component({
  selector: 'lib-test-root',
  imports: [OnPushAncestorComponent],
  template: '<lib-test-on-push-ancestor />',
})
class RootComponent {}

describe('ContentControlComponent under an OnPush ancestor', () => {
  const contents$ = new Subject<{ items: ContentDto[] }>();

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: ContentAdminService, useValue: { getList: () => contents$ } }],
    });
  });

  it('renders the options once the response arrives, with no event to mark the ancestors', () => {
    const fixture = TestBed.createComponent(RootComponent);
    fixture.detectChanges();
    const optionTexts = () =>
      [...fixture.nativeElement.querySelectorAll('select option')].map((option: HTMLOptionElement) =>
        option.textContent?.trim(),
      );
    expect(optionTexts()).toEqual(['']);

    contents$.next({
      items: [
        { id: 'content-1', slug: 'first' },
        { id: 'content-2', slug: 'second' },
      ] as ContentDto[],
    });
    fixture.detectChanges();

    expect(optionTexts()).toEqual(['', 'first', 'second']);
  });
});
