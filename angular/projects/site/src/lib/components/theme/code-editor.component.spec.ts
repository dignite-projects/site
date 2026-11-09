import { LocalizationService } from '@abp/ng.core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EditorState } from '@codemirror/state';
import { EditorView, logException } from '@codemirror/view';
import { CodeEditorComponent } from './code-editor.component';

describe('CodeEditorComponent', () => {
  const windowsFile = '<div>\r\n  {{ page.title }}\r\n</div>\r\n';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: LocalizationService, useValue: { instant: (key: string) => key } }],
    });
  });

  afterEach(() => vi.restoreAllMocks());

  function render(value: string, documentKey = 'Events') {
    const fixture = TestBed.createComponent(CodeEditorComponent);
    const emitted: string[] = [];
    fixture.componentInstance.valueChange.subscribe(text => emitted.push(text));
    fixture.componentRef.setInput('documentKey', documentKey);
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();
    // The effect runs again once the view exists, after ngAfterViewInit.
    fixture.detectChanges();
    return { fixture, emitted };
  }

  const set = (fixture: ComponentFixture<CodeEditorComponent>, inputs: Record<string, string>) => {
    Object.entries(inputs).forEach(([name, value]) => fixture.componentRef.setInput(name, value));
    fixture.detectChanges();
  };

  it('does not take a file written with \\r\\n for an edit when it opens it', () => {
    const { fixture, emitted } = render(windowsFile);
    set(fixture, { value: windowsFile });

    expect(emitted).toEqual([]);
  });

  it('does not take a file written with \\r\\n for an edit when it switches to it', () => {
    const { fixture, emitted } = render('<p>x</p>\n', 'Other');
    set(fixture, { documentKey: 'Events', value: windowsFile });
    fixture.detectChanges();

    expect(emitted).toEqual([]);
  });

  it('writes an edit of a file written with \\r\\n back with \\r\\n', () => {
    const { fixture, emitted } = render(windowsFile);

    fixture.componentInstance.insert('<!-- a -->\n');

    expect(emitted).toEqual(['<!-- a -->\r\n' + windowsFile]);
  });

  it('comes back from its own edit as a no-op', () => {
    const { fixture, emitted } = render(windowsFile);
    fixture.componentInstance.insert('x');
    set(fixture, { value: emitted[0] });

    expect(emitted).toEqual(['x' + windowsFile]);
  });

  it('puts a reloaded file in without taking it for an edit, whatever its line breaks', () => {
    const { fixture, emitted } = render(windowsFile);
    set(fixture, { value: '<div>\r\n  changed\n</div>' });

    expect(emitted).toEqual([]);

    fixture.componentInstance.insert('x');
    expect(emitted).toEqual(['x<div>\n  changed\n</div>']);
  });

  it('writes an edit of a file written with \\n back with \\n', () => {
    const { fixture, emitted } = render('<p>\n</p>');

    fixture.componentInstance.insert('x\r\n');

    expect(emitted).toEqual(['x\n<p>\n</p>']);
  });

  describe('when it fails', () => {
    // What CodeMirror throws when the application loaded two copies of @codemirror/state.
    const duplicateState = 'Unrecognized extension value in extension set ([object Object]). This sometimes happens '
      + 'because multiple instances of @codemirror/state are loaded, breaking instanceof checks.';

    const alertOf = (fixture: ComponentFixture<CodeEditorComponent>) =>
      (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent ?? '';
    const hostOf = (fixture: ComponentFixture<CodeEditorComponent>) =>
      (fixture.nativeElement as HTMLElement).lastElementChild as HTMLElement;

    beforeEach(() => vi.spyOn(console, 'error').mockImplementation(() => undefined));

    it('says why where the editor would be when it cannot open a file, and edits nothing', () => {
      vi.spyOn(EditorState, 'create').mockImplementationOnce(() => {
        throw new Error(duplicateState);
      });

      const { fixture, emitted } = render('<p>x</p>');
      fixture.componentInstance.insert('y');

      expect(alertOf(fixture)).toContain('Site::CodeEditorFailed');
      expect(alertOf(fixture)).toContain('Site::CodeEditorFailedHint');
      expect(alertOf(fixture)).toContain('multiple instances of @codemirror/state');
      expect(hostOf(fixture).hidden).toBe(true);
      expect(emitted).toEqual([]);
    });

    it('tries again when another file is opened', () => {
      vi.spyOn(EditorState, 'create').mockImplementationOnce(() => {
        throw new Error(duplicateState);
      });

      const { fixture, emitted } = render('<p>x</p>');
      set(fixture, { documentKey: 'Other', value: '<p>other</p>' });
      fixture.componentInstance.insert('y');

      expect(alertOf(fixture)).toBe('');
      expect(hostOf(fixture).hidden).toBe(false);
      expect(emitted).toEqual(['y<p>other</p>']);
    });

    it('shows an exception CodeMirror caught after the file opened, and keeps the editor', () => {
      const { fixture, emitted } = render('<p>x</p>');
      const view = EditorView.findFromDOM((fixture.nativeElement as HTMLElement).querySelector('.cm-editor') as HTMLElement);

      logException(view!.state, new Error('CodeMirror plugin crashed'));
      fixture.detectChanges();
      fixture.componentInstance.insert('y');

      expect(alertOf(fixture)).toContain('CodeMirror plugin crashed');
      expect(hostOf(fixture).hidden).toBe(false);
      expect(emitted).toEqual(['y<p>x</p>']);
    });
  });
});
