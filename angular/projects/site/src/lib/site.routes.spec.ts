import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, CanDeactivateFn, Route, RouterStateSnapshot } from '@angular/router';
import { SITE_ROUTES } from './site.routes';

describe('SITE_ROUTES', () => {
  const children: Route[] = SITE_ROUTES[0].children ?? [];

  const confirming = ['contents/create', 'contents/:id/edit', 'theme'];

  it.each(confirming)('asks the page before %s is left', path => {
    const guards = children.find(route => route.path === path)?.canDeactivate ?? [];
    expect(guards).toHaveLength(1);

    const canLeave = vi.fn().mockReturnValue(false);
    const guard = guards[0] as CanDeactivateFn<{ canLeave: () => boolean }>;
    const answer = TestBed.runInInjectionContext(() =>
      guard(
        { canLeave },
        {} as ActivatedRouteSnapshot,
        {} as RouterStateSnapshot,
        {} as RouterStateSnapshot,
      ),
    );

    expect(canLeave).toHaveBeenCalledOnce();
    expect(answer).toBe(false);
  });

  it('puts no such guard on the other pages', () => {
    const others = children.filter(route => !confirming.includes(route.path ?? ''));

    expect(others.some(route => route.canDeactivate?.length)).toBe(false);
  });
});
