import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Route, RouterStateSnapshot } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { AppRole } from '../models/auth.model';
import { routes } from '../app.routes';
import { roleGuard } from './role.guard';

describe('roleGuard on application routes', () => {
  const children: Route[] = routes[0].children ?? [];

  function canActivate(path: string, roles: string[]): boolean {
    const route = children.find(candidate => candidate.path === path);
    expect(route).withContext(`route ${path}`).toBeDefined();
    const auth = {
      isAuthenticated: true,
      hasRole: (role: string) => roles.includes(role),
      login: jasmine.createSpy('login')
    };
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: auth }] });
    const snapshot = { data: route!.data } as unknown as ActivatedRouteSnapshot;
    return TestBed.runInInjectionContext(() => roleGuard(snapshot, {} as RouterStateSnapshot)) as boolean;
  }

  const salesRoutes = [
    'vendite/cassa', 'ordini/elenco', 'ordini/nuovo', 'fatturazione/fatture', 'fatturazione/nota-credito',
    'magazzino/inventario'
  ];

  for (const path of salesRoutes) {
    it(`allows Sales on ${path}`, () => {
      expect(canActivate(path, ['Sales'])).toBeTrue();
      expect(canActivate(path, ['Admin'])).toBeTrue();
    });
  }

  it('keeps the cassa and orders closed to Warehouse and Accounting', () => {
    for (const path of ['vendite/cassa', 'ordini/elenco', 'ordini/nuovo']) {
      expect(canActivate(path, ['Warehouse'])).withContext(path).toBeFalse();
      expect(canActivate(path, ['Accounting'])).withContext(path).toBeFalse();
    }
  });

  it('allows Accounting on invoices and credit notes but blocks Warehouse', () => {
    for (const path of ['fatturazione/fatture', 'fatturazione/nota-credito']) {
      expect(canActivate(path, ['Accounting'])).withContext(path).toBeTrue();
      expect(canActivate(path, ['Warehouse'])).withContext(path).toBeFalse();
    }
  });

  it('allows Admin and Warehouse on the DDT list but not Sales', () => {
    expect(canActivate('ddt/elenco', ['Admin'])).toBeTrue();
    expect(canActivate('ddt/elenco', ['Warehouse'])).toBeTrue();
    expect(canActivate('ddt/elenco', ['Sales'])).toBeFalse();
  });

  it('keeps the current stock page open to Admin and Warehouse and closed to Accounting', () => {
    expect(canActivate('magazzino/inventario', ['Warehouse'])).toBeTrue();
    expect(canActivate('magazzino/inventario', ['Accounting'])).toBeFalse();
  });

  it('no longer grants any route to the removed Cashier role', () => {
    const allowedRoles = children.flatMap(route => (route.data?.['roles'] as string[] | undefined) ?? []);
    expect(allowedRoles).not.toContain('Cashier');
    expect(canActivate('vendite/cassa', ['Cashier'])).toBeFalse();
  });

  it('does not accept Cashier as an AppRole', () => {
    const valid: AppRole[] = ['Admin', 'Warehouse', 'Sales', 'Accounting'];
    // @ts-expect-error Cashier was merged into Sales
    const removed: AppRole = 'Cashier';
    expect(valid).not.toContain(removed);
  });
});
