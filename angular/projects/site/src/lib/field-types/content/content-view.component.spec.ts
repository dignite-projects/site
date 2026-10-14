import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { ContentAdminService } from '../../proxy/dignite/site/admin/contents/content-admin.service';
import type { ContentDto } from '../../proxy/dignite/site/contents/models';
import { ContentViewComponent } from './content-view.component';

/**
 * The content list renders this view in its page flow, under the host layout's OnPush ancestors
 * (`abp-router-outlet`, the LeptonX layouts). A plain-field write in the HTTP callback never marks those
 * for check, so the cell stayed at `-` until an unrelated event did. See `content-types.component.spec.ts`.
 */
@Component({
  selector: 'lib-test-on-push-ancestor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ContentViewComponent],
  template: '<site-content-view [showInList]="true" [value]="ids" />',
})
class OnPushAncestorComponent {
  readonly ids = ['content-1'];
}

@Component({
  selector: 'lib-test-root',
  imports: [OnPushAncestorComponent],
  template: '<lib-test-on-push-ancestor />',
})
class RootComponent {}

describe('ContentViewComponent under an OnPush ancestor', () => {
  const content$ = new Subject<ContentDto>();

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: ContentAdminService, useValue: { get: () => content$ } }],
    });
  });

  it('shows the resolved content once the response arrives, with no event to mark the ancestors', () => {
    const fixture = TestBed.createComponent(RootComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent.trim()).toBe('-');

    content$.next({ id: 'content-1', slug: 'hello-world' } as ContentDto);
    content$.complete();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent.trim()).toBe('hello-world');
  });
});
