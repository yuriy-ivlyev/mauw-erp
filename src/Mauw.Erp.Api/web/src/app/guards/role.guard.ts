import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const roleGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const auth = inject(AuthService);
  const requiredRoles: string[] = route.data['roles'] ?? [];

  if (!auth.isAuthenticated) {
    auth.login();
    return false;
  }

  return requiredRoles.length === 0 || requiredRoles.some(r => auth.hasRole(r));
};
