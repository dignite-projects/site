import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import type { FlexFieldValue } from '@dignite/ng.flex-fields';
import { readStringList } from '@dignite/ng.flex-fields';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ContentAdminService } from '../../proxy/dignite/site/admin/contents/content-admin.service';
import type { ContentDto } from '../../proxy/dignite/site/contents/models';

/**
 * Displays the value of a `Content` field read-only: the referenced Content, resolved by id. An id
 * that no longer resolves (deleted since picked) is silently dropped rather than shown as an error -
 * the same "fail open" stance the server's own `Content.cshtml` partial takes.
 */
@Component({
  selector: 'site-content-view',
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './content-view.component.html',
  imports: [CommonModule],
})
export class ContentViewComponent implements OnChanges {
  private readonly contentAdminService = inject(ContentAdminService);

  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type - always `Content` here. */
  @Input() type?: string;

  @Input() value: unknown = '';

  /**
   * A signal, not a plain field: it is filled by an HTTP callback, and this view renders in the content
   * list's page flow, under the host layout's OnPush ancestors - a plain-field write there never marks
   * them for check, so the cell would stay at `-` until an unrelated event.
   */
  readonly resolved = signal<ContentDto[]>([]);

  get summaryText(): string {
    const resolved = this.resolved();
    return resolved.length === 0 ? '-' : resolved.map(item => item.slug).join(', ');
  }

  ngOnChanges(): void {
    const ids = readStringList(this.value).filter(id => id !== '');

    if (ids.length === 0) {
      this.resolved.set([]);
      return;
    }

    forkJoin(ids.map(id => this.contentAdminService.get(id).pipe(catchError(() => of(null))))).subscribe(
      results => {
        this.resolved.set(results.filter((item): item is ContentDto => item !== null));
      },
    );
  }
}
