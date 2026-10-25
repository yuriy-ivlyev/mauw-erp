import { Route } from '@angular/router';
import { routes } from '../app.routes';
import { authGuard } from '../guards/auth.guard';
import { roleGuard } from '../guards/role.guard';
import { MenuService } from './menu.service';

describe('Price formula navigation', () => {
  it('registers the route behind authGuard and an Admin-only roleGuard', async () => {
    const root = routes.find(route => route.path === '');
    expect(root?.canActivate).toContain(authGuard);
    const route = root?.children?.find((child: Route) => child.path === 'impostazioni/formula-prezzi');
    expect(route).toBeDefined();
    expect(route?.canActivate).toEqual([roleGuard]);
    expect(route?.data?.['roles']).toEqual(['Admin']);
    const component = await (route?.loadComponent as () => Promise<unknown>)();
    expect(component).toBeDefined();
  });

  it('lists "Formula prezzi" right after "Azienda" for Admin only', () => {
    const settings = new MenuService().getMenuItems().find(item => item.label === 'Sistema');
    const children = settings?.children ?? [];
    const azienda = children.findIndex(item => item.label === 'Azienda');
    expect(azienda).toBeGreaterThan(-1);
    const entry = children[azienda + 1];
    expect(entry.label).toBe('Formula prezzi');
    expect(entry.route).toBe('/impostazioni/formula-prezzi');
    expect(entry.roles).toEqual(['Admin']);
  });
});
