import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CodeEditorComponent } from './code-editor.component';

describe('CodeEditorComponent', () => {
  const windowsFile = '<div>\r\n  {{ page.title }}\r\n</div>\r\n';

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
});
