import { LocalizationPipe } from '@abp/ng.core';
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
  untracked,
  viewChild,
} from '@angular/core';
import { css } from '@codemirror/lang-css';
import { html } from '@codemirror/lang-html';
import { javascript } from '@codemirror/lang-javascript';
import { ensureSyntaxTree } from '@codemirror/language';
import { Annotation, Compartment, EditorState, Extension } from '@codemirror/state';
import { EditorView } from '@codemirror/view';
import { basicSetup } from 'codemirror';
import { lineBreakOf, type ThemeEditorLanguage } from './theme-workbench.helpers';

/** Marks the change that puts a new {@link CodeEditorComponent.value} in the editor: nobody edited the document. */
const VALUE_SET = Annotation.define<true>();

/**
 * How long, in milliseconds, opening a file may spend parsing the rest of it. CodeMirror parses only the start of a
 * document when it creates the state and the rest in idle callbacks, where an exception reaches nobody but the console.
 */
const PARSE_CHECK_TIMEOUT = 50;

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
 *
 * An editor that fails says so where the editor would be, rather than leaving the space blank: CodeMirror and Lezer
 * work only with a single copy of each of their packages on the page, and an application whose package manager
 * installed two gets an exception from deep inside them. When opening a file fails - creating the editing state, or
 * parsing the file - the editor is hidden and edits nothing until another file is opened or the file is reloaded, so an
 * edit can never land in the wrong file. An exception CodeMirror catches later, in a plugin or a listener, is shown
 * too, and the editor stays as it is.
 */
@Component({
  selector: 'site-code-editor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe],
  template: `
    @if (problem(); as message) {
      <div class="alert alert-danger mb-2" role="alert">
        <strong class="d-block mb-1">{{ 'Site::CodeEditorFailed' | abpLocalization }}</strong>
        <p class="mb-1">{{ 'Site::CodeEditorFailedHint' | abpLocalization }}</p>
        <code class="d-block small text-break">{{ message }}</code>
      </div>
    }
    <div #host [hidden]="unusable()"></div>
  `,
  styles: ':host { display: block; }',
})
export class CodeEditorComponent implements AfterViewInit, OnDestroy {
  readonly value = input('');
  readonly readonly = input(false);
  readonly language = input<ThemeEditorLanguage>('html');
  readonly documentKey = input<string | undefined>(undefined);
  readonly valueChange = output<string>();

  /** The message of the exception the editor last ran into, while it stands. */
  protected readonly problem = signal<string | undefined>(undefined);
  /** Opening the current file failed: the editor is hidden and edits nothing. */
  protected readonly unusable = signal(false);

  private readonly host = viewChild.required<ElementRef<HTMLElement>>('host');
  private readonly readOnlyCompartment = new Compartment();
  private readonly languageCompartment = new Compartment();
  private currentKey: string | undefined;
  // The text the document stands for - the value last given or the text last emitted - and its line break.
  private text = '';
  private lineBreak: '\n' | '\r\n' = '\n';
  private view: EditorView | undefined;

  // A signal, so the effects below run again once the host element exists - after their first run.
  private readonly mounted = signal(false);

  constructor() {
    effect(() => {
      if (!this.mounted()) {
        return;
      }

      const key = this.documentKey();
      const value = this.value();

      untracked(() => {
        if (!this.view || this.unusable() || key !== this.currentKey) {
          this.currentKey = key;
          this.open(value);
        } else if (value !== this.text) {
          this.hold(value);
          this.run(view =>
            view.dispatch({
              changes: { from: 0, to: view.state.doc.length, insert: value },
              annotations: VALUE_SET.of(true),
            }),
          );
        }
      });
    });

    effect(() => {
      if (!this.mounted()) {
        return;
      }

      const readonly = this.readonly();
      const language = this.language();

      untracked(() =>
        this.run(view =>
          view.dispatch({
            effects: [
              this.readOnlyCompartment.reconfigure(readOnlyExtensions(readonly)),
              this.languageCompartment.reconfigure(languageExtension(language)),
            ],
          }),
        ),
      );
    });
  }

  ngAfterViewInit(): void {
    this.mounted.set(true);
  }

  /** Starts a new editing state for `doc`, creating the editor the first time. */
  private open(doc: string): void {
    this.problem.set(undefined);
    this.unusable.set(false);
    try {
      const state = this.createState(doc);
      if (this.view) {
        this.view.setState(state);
      } else {
        this.view = new EditorView({ parent: this.host().nativeElement, state });
      }
    } catch (error) {
      this.unusable.set(true);
      this.report(error);
    }
  }

  /** Runs `action` on a usable editor; an exception makes the editor unusable until a file is opened again. */
  private run(action: (view: EditorView) => void): void {
    const view = this.view;
    if (!view || this.unusable()) {
      return;
    }

    try {
      action(view);
    } catch (error) {
      this.unusable.set(true);
      this.report(error);
    }
  }

  private report(error: unknown): void {
    console.error(error);
    this.problem.set(error instanceof Error ? error.message : String(error));
  }

  private hold(text: string): void {
    this.text = text;
    this.lineBreak = lineBreakOf(text);
  }

  private createState(doc: string): EditorState {
    this.hold(doc);
    const state = EditorState.create({
      doc,
      extensions: [
        basicSetup,
        this.languageCompartment.of(languageExtension(this.language())),
        EditorView.lineWrapping,
        EDITOR_THEME,
        this.readOnlyCompartment.of(readOnlyExtensions(this.readonly())),
        EditorView.exceptionSink.of(error => this.report(error)),
        EditorView.updateListener.of(update => {
          if (update.transactions.some(tr => tr.docChanged && !tr.annotation(VALUE_SET))) {
            this.text = update.state.doc.sliceString(0, undefined, this.lineBreak);
            this.valueChange.emit(this.text);
          }
        }),
      ],
    });

    // Parse the rest of the file now, where an exception is caught, rather than in CodeMirror's idle callbacks.
    ensureSyntaxTree(state, state.doc.length, PARSE_CHECK_TIMEOUT);
    return state;
  }

  ngOnDestroy(): void {
    this.view?.destroy();
  }

  /** Puts the cursor at a 1-based line and column and scrolls it into view. Out-of-range values are clamped. */
  goTo(line: number, column: number): void {
    const view = this.view;
    if (!view || this.unusable()) {
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
    const view = this.view;
    if (!view || this.unusable() || this.readonly()) {
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
