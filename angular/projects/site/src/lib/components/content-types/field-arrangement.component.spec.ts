import { DefaultQueueManager, QUEUE_MANAGER } from '@abp/ng.core';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FieldTypeResolver, FlexFieldsStyleLoader } from '@dignite/ng.flex-fields';
import type { ContentTypeFieldDto } from '../../proxy/dignite/site/content-types/models';
import type { FieldDto } from '../../proxy/dignite/site/fields/models';
import { FieldArrangementComponent } from './field-arrangement.component';

describe('FieldArrangementComponent', () => {
  const field = (id: string, name: string) =>
    ({ id, name, displayName: name, fieldTypeName: 'Text' }) as FieldDto;

  const usage = (fieldId: string, order: number): ContentTypeFieldDto => ({
    fieldId,
    required: false,
    searchable: true,
    showInList: true,
    displayName: null,
    order,
  });

  async function render(options: {
    arrangement: ContentTypeFieldDto[];
    library: FieldDto[];
    loaded: boolean;
  }) {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig()],
      providers: [
        { provide: QUEUE_MANAGER, useClass: DefaultQueueManager },
        { provide: FieldTypeResolver, useValue: { getAll: () => [] } },
        { provide: FlexFieldsStyleLoader, useValue: { load: vi.fn() } },
      ],
    });

    const fixture = TestBed.createComponent(FieldArrangementComponent);
    fixture.componentRef.setInput('arrangement', options.arrangement);
    fixture.componentRef.setInput('fieldsById', new Map(options.library.map(f => [f.id!, f])));
    fixture.componentRef.setInput('fieldsLoaded', options.loaded);
    fixture.detectChanges();
    // ngModel applies a disabled state in a microtask, after the first render.
    await fixture.whenStable();
    fixture.detectChanges();

    return fixture;
  }

  const rows = (fixture: ComponentFixture<FieldArrangementComponent>) =>
    Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll('tbody tr'));

  const toggles = (row: HTMLElement) =>
    Array.from(row.querySelectorAll<HTMLInputElement>('input[type=checkbox]'));

  it('marks a usage whose field is gone as deleted and turns its inputs off, leaving a live one alone', async () => {
    const fixture = await render({
      arrangement: [usage('live', 0), usage('gone', 1)],
      library: [field('live', 'title')],
      loaded: true,
    });
    const [live, deleted] = rows(fixture);

    // No localization is loaded here, so the pipe hands back the key without its resource name.
    expect(live.querySelector('small.text-danger')).toBeNull();
    expect(live.textContent).not.toContain('DeletedField');
    expect(toggles(live).every(toggle => !toggle.disabled)).toBe(true);

    expect(deleted.querySelector('small.text-danger')?.textContent).toBe('DeletedField');
    expect(deleted.querySelector('small.d-block')?.textContent).toBe('DeletedFieldHint');
    expect(toggles(deleted)).toHaveLength(3);
    expect(toggles(deleted).every(toggle => toggle.disabled)).toBe(true);
    expect(deleted.querySelector<HTMLInputElement>('input[type=text]')!.disabled).toBe(true);
  });

  it('still lets the user remove the deleted row', async () => {
    const arrangement = [usage('live', 0), usage('gone', 1)];
    const fixture = await render({ arrangement, library: [field('live', 'title')], loaded: true });

    rows(fixture)[1].querySelector<HTMLButtonElement>('button')!.click();
    fixture.detectChanges();

    expect(arrangement.map(entry => entry.fieldId)).toEqual(['live']);
    expect(rows(fixture)).toHaveLength(1);
  });

  it('calls nothing deleted before the library has loaded', async () => {
    const fixture = await render({ arrangement: [usage('a', 0)], library: [], loaded: false });

    expect(fixture.nativeElement.textContent).not.toContain('DeletedField');
    expect(toggles(rows(fixture)[0]).every(toggle => !toggle.disabled)).toBe(true);
  });
});
