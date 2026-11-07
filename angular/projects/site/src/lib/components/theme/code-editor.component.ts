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
import { css } from '@codemirror/lang-css';
import { html } from '@codemirror/lang-html';
import { javascript } from '@codemirror/lang-javascript';
import { Annotation, Compartment, EditorState, Extension } from '@codemirror/state';
import { EditorView } from '@codemirror/view';
import { basicSetup } from 'codemirror';
import { lineBreakOf, type ThemeEditorLanguage } from './theme-workbench.helpers';

/** Marks the change that puts a new {@link CodeEditorComponent.value} in the editor: nobody edited the document. */
const VALUE_SET = Annotation.define<true>();

/**
 * A code editor over CodeMirror 6, used for the files of a theme.
 *
 * A template is highlighted as HTML, not Scriban: it is an HTML document with Scriban's `{{ }}` in it, and there is
 * no Scriban grammar for CodeMirror, so the `{{ }}` parts are not highlighted - only the markup around them. A static
 * file is highlighted by its extension ({@link language}).
 *
 * {@link documentKey} names the file being edited. When it changes, the editor starts a new editing state for the new
 * file, so undo never reaches into another file. While it stays the same, the document is owned by CodeMirror as the
 * user types; {@link value} only matters when it differs from the text the editor was last given or last emitted (the
 * file was reloaded, say), in which case the whole document is replaced - without a {@link valueChange}, as nobody
 * edited it. A value the editor itself just emitted therefore comes back as a no-op.
 *
 * CodeMirror reads every line break as `\n`, so the document of a file written with `\r\n` is not the file's text. The
 * editor never compares the two: it compares {@link value} with the text it holds, and emits the document with the
 * file's own line break ({@link lineBreakOf}). Opening a file is not an edit, and editing a line of it changes that line.
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
  readonly language = input<ThemeEditorLanguage>('html');
  readonly documentKey = input<string | undefined>(undefined);
  readonly valueChange = output<string>();

  private readonly host = viewChild.required<ElementRef<HTMLElement>>('host');
  private readonly readOnlyCompartment = new Compartment();
  private readonly languageCompartment = new Compartment();
  private currentKey: string | undefined;
  // The text the document stands for - the value last given or the text last emitted - and its line break.
  private text = '';
  private lineBreak: '\n' | '\r\n' = '\n';

  // A signal, so the effects below run again once the view exists - it is created in ngAfterViewInit, after
  // their first run.
  private readonly view = signal<EditorView | undefined>(undefined);

  constructor() {
    effect(() => {
      const view = this.view();
      const key = this.documentKey();
      const value = this.value();

      if (!view) {
        return;
      }

      if (key !== this.currentKey) {
        this.currentKey = key;
        view.setState(this.createState(value));
        return;
      }

      if (value !== this.text) {
        this.hold(value);
        view.dispatch({
          changes: { from: 0, to: view.state.doc.length, insert: value },
          annotations: VALUE_SET.of(true),
        });
      }
    });

    effect(() => {
      const view = this.view();
      const readonly = this.readonly();
      const language = this.language();

      view?.dispatch({
        effects: [
          this.readOnlyCompartment.reconfigure(readOnlyExtensions(readonly)),
          this.languageCompartment.reconfigure(languageExtension(language)),
        ],
      });
    });
  }

  ngAfterViewInit(): void {
    this.currentKey = this.documentKey();
    this.view.set(new EditorView({ parent: this.host().nativeElement, state: this.createState(this.value()) }));
  }

  private hold(text: string): void {
    this.text = text;
    this.lineBreak = lineBreakOf(text);
  }

  private createState(doc: string): EditorState {
    this.hold(doc);
    return EditorState.create({
      doc,
      extensions: [
        basicSetup,
        this.languageCompartment.of(languageExtension(this.language())),
        EditorView.lineWrapping,
        EDITOR_THEME,
        this.readOnlyCompartment.of(readOnlyExtensions(this.readonly())),
        EditorView.updateListener.of(update => {
          if (update.transactions.some(tr => tr.docChanged && !tr.annotation(VALUE_SET))) {
            this.text = update.state.doc.sliceString(0, undefined, this.lineBreak);
            this.valueChange.emit(this.text);
          }
        }),
      ],
    });
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

  /**
   * Replaces the selection (or inserts at the cursor) with `text`, puts the cursor after it and focuses the editor. It
   * is an ordinary transaction, so {@link valueChange} fires exactly as it does for typing. A read-only editor is left
   * alone.
   */
  insert(text: string): void {
    const view = this.view();
    if (!view || this.readonly()) {
      return;
    }

    view.dispatch(view.state.update(view.state.replaceSelection(text), { scrollIntoView: true, userEvent: 'input' }));
    view.focus();
  }
}

function readOnlyExtensions(readonly: boolean) {
  return [EditorState.readOnly.of(readonly), EditorView.editable.of(!readonly)];
}

function languageExtension(language: ThemeEditorLanguage): Extension {
  switch (language) {
    case 'html':
      return html();
    case 'css':
      return css();
    case 'javascript':
      return javascript();
    default:
      return [];
  }
}

// The editor sits inside a Bootstrap card, so it inherits the card's colors rather than reading the body's:
// in LeptonX Lite's dim theme --bs-body-bg is dark while the card stays light, and an editor painted with the
// body tokens came out as a black box on a white card. Only the chrome (border, gutter, active line) reads
// theme variables, with Bootstrap's light values as fallbacks.
const EDITOR_THEME = EditorView.theme({
  // The page that hosts the editor may size it: --site-code-editor-height fixes its height, and without it the
  // editor grows with its text up to --site-code-editor-max-height.
  '&': {
    height: 'var(--site-code-editor-height, auto)',
    minHeight: '24rem',
    maxHeight: 'var(--site-code-editor-max-height, 75vh)',
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
