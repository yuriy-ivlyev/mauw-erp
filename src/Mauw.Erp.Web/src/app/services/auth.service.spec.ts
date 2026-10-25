import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';

describe('AuthService cost visibility', () => {
  async function serviceFor(roles: string[] | undefined): Promise<AuthService> {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(AuthService);
    const initialized = service.initialize();
    TestBed.inject(HttpTestingController).expectOne('/api/auth/user')
      .flush({ isAuthenticated: true, roles });
    await initialized;
    return service;
  }

  it('shows costs to Admin and Accounting', async () => {
    for (const role of ['Admin', 'Accounting']) {
      TestBed.resetTestingModule();
      const service = await serviceFor([role]);
      expect(service.canSeeCosts()).withContext(role).toBeTrue();
    }
  });

  it('hides costs from Sales and Warehouse', async () => {
    for (const role of ['Sales', 'Warehouse']) {
      TestBed.resetTestingModule();
      const service = await serviceFor([role]);
      expect(service.canSeeCosts()).withContext(role).toBeFalse();
    }
  });

  it('hides costs when the user has no roles', async () => {
    const service = await serviceFor(undefined);
    expect(service.canSeeCosts()).toBeFalse();
  });
});
