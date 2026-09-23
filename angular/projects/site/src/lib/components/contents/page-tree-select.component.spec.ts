import { ElementRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { PageDto } from '../../proxy/dignite/site/pages/models';
import { PageTreeSelectComponent } from './page-tree-select.component';

describe('PageTreeSelectComponent', () => {
  const pages: PageDto[] = [
    { id: 'home', displayName: 'Home', parentId: null },
    { id: 'about', displayName: 'About', parentId: null },
    { id: 'team', displayName: 'Team', parentId: 'about' },
    { id: 'jobs', name: 'jobs', parentId: 'team' },
    // Its parent is not in the list.
    { id: 'stray', displayName: 'Stray', parentId: 'gone' },
  ];

  function create(): PageTreeSelectComponent {
    TestBed.configureTestingModule({
      providers: [{ provide: ElementRef, useValue: new ElementRef(document.createElement('div')) }],
    });
    const component = TestBed.runInInjectionContext(() => new PageTreeSelectComponent());
    component.pages = pages;
    return component;
  }

  it('rebuilds the hierarchy from parentId', () => {
    const component = create();

    expect(component.nodes.map(node => node.key)).toEqual(['home', 'about', 'stray']);
    const about = component.nodes[1];
    expect(about.children.map(node => node.key)).toEqual(['team']);
    expect(about.children[0].children.map(node => node.key)).toEqual(['jobs']);
  });

  it('shows a page whose parent is missing at the root instead of dropping it', () => {
    const component = create();

    expect(component.nodes.some(node => node.key === 'stray')).toBe(true);
  });

  it('falls back to the page name when it has no display name', () => {
    const component = create();
    component.value = 'jobs';

    expect(component.selectedNode?.title).toBe('jobs');
  });

  it('opens with every branch expanded', () => {
    const component = create();

    expect(component.expandedKeys.sort()).toEqual(['about', 'team']);
  });

  it('emits the picked page and closes', () => {
    const component = create();
    const emitted: (string | null)[] = [];
    component.valueChange.subscribe(value => emitted.push(value));
    component.toggle();

    component.select(component.nodes[1].entity);

    expect(emitted).toEqual(['about']);
    expect(component.isOpen).toBe(false);
  });

  it('does not emit when the already-selected page is picked again', () => {
    const component = create();
    component.value = 'about';
    const emitted: (string | null)[] = [];
    component.valueChange.subscribe(value => emitted.push(value));

    component.select(component.nodes[1].entity);

    expect(emitted).toEqual([]);
  });

  it('emits null on clear', () => {
    const component = create();
    component.value = 'about';
    const emitted: (string | null)[] = [];
    component.valueChange.subscribe(value => emitted.push(value));

    component.clear(new Event('click'));

    expect(emitted).toEqual([null]);
  });
});
