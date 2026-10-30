import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  effect,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { html } from '@codemirror/lang-html';
import { Compartment, EditorState } from '@codemirror/state';
import { EditorView } from '@codemirror/view';
import { basicSetup } from 'codemirror';

/**
 * A code editor over CodeMirror 6, used for a template's source.
 *
 * The language is HTML, not Scriban: a template is an HTML document with Scriban's `{{ }}` in it, and there is
 * no Scriban grammar for CodeMirror, so the `{{ }}` parts are not highlighted - only the markup around them.
 *
 * The document is owned by CodeMirror while the user types; {@link value} only matters when it differs from the
 * document (the parent loaded another version, say), in which case the whole document is replaced. A value the
 * editor itself just emitted therefore comes back as a no-op.
 */
@Component({
  selector: 'site-code-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '<div #host></div>',
  styles: ':host { display: block; }',
})
export class CodeEditorComponent implements AfterViewInit, OnDestroy {
  readonly value = input('');
  readonly readonly = input(false);
  readonly valueChange = output<string>();

  private readonly host = viewChild.required<ElementRef<HTMLElement>>('host');
  private readonly readOnlyCompartment = new Compartment();

  // A signal, so the effect below runs again once the view exists - it is created in ngAfterViewInit, after
  // the effect's first run.
  private readonly view = signal<EditorView | undefined>(undefined);

  constructor() {
    effect(() => {
      const view = this.view();
      const value = this.value();

      if (view && value !== view.state.doc.toString()) {
        view.dispatch({ changes: { from: 0, to: view.state.doc.length, insert: value } });
      }
    });

    effect(() => {
      const view = this.view();
      const readonly = this.readonly();

      view?.dispatch({
        effects: this.readOnlyCompartment.reconfigure(readOnlyExtensions(readonly)),
      });
    });
  }

  ngAfterViewInit(): void {
    this.view.set(
      new EditorView({
        parent: this.host().nativeElement,
        state: EditorState.create({
          doc: this.value(),
          extensions: [
            basicSetup,
            html(),
            EditorView.lineWrapping,
            EDITOR_THEME,
            this.readOnlyCompartment.of(readOnlyExtensions(this.readonly())),
            EditorView.updateListener.of(update => {
              if (update.docChanged) {
                this.valueChange.emit(update.state.doc.toString());
              }
            }),
          ],
        }),
      }),
    );
  }

  ngOnDestroy(): void {
    this.view()?.destroy();
  }

  /** Puts the cursor at a 1-based line and column and scrolls it into view. Out-of-range values are clamped. */
  goTo(line: number, column: number): void {
    const view = this.view();
    if (!view) {
      return;
    }

    const doc = view.state.doc;
    const target = doc.line(Math.min(Math.max(Math.trunc(line) || 1, 1), doc.lines));
    const position = Math.min(target.from + Math.max(Math.trunc(column) - 1, 0), target.to);

    view.dispatch({
      selection: { anchor: position },
      effects: EditorView.scrollIntoView(position, { y: 'center' }),
    });
    view.focus();
  }
}

function readOnlyExtensions(readonly: boolean) {
  return [EditorState.readOnly.of(readonly), EditorView.editable.of(!readonly)];
}

// The editor sits inside a Bootstrap card, so it inherits the card's colors rather than reading the body's:
// in LeptonX Lite's dim theme --bs-body-bg is dark while the card stays light, and an editor painted with the
// body tokens came out as a black box on a white card. Only the chrome (border, gutter, active line) reads
// theme variables, with Bootstrap's light values as fallbacks.
const EDITOR_THEME = EditorView.theme({
  '&': {
    minHeight: '24rem',
    maxHeight: '75vh',
    border: '1px solid var(--bs-border-color, #dee2e6)',
    borderRadius: 'var(--bs-border-radius, 0.375rem)',
    backgroundColor: 'transparent',
  },
  '&.cm-focused': {
    outline: 'none',
    borderColor: 'var(--bs-primary, #0d6efd)',
  },
  '.cm-scroller': {
    minHeight: '24rem',
    fontFamily: 'var(--bs-font-monospace, monospace)',
  },
  '.cm-gutters': {
    backgroundColor: 'transparent',
    color: 'inherit',
    opacity: '0.6',
    border: 'none',
    borderRight: '1px solid var(--bs-border-color, #dee2e6)',
  },
  '.cm-activeLine, .cm-activeLineGutter': {
    backgroundColor: 'rgba(128, 128, 128, 0.12)',
  },
});
