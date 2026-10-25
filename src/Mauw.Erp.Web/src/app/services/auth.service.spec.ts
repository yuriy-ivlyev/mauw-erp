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

  it('shows costs and margins to Admin', async () => {
    const service = await serviceFor(['Admin']);
    expect(service.canSeeCosts()).toBeTrue();
    expect(service.canSeeMargins()).toBeTrue();
  });

  it('shows costs but not margins to Accounting', async () => {
    const service = await serviceFor(['Accounting']);
    expect(service.canSeeCosts()).toBeTrue();
    expect(service.canSeeMargins()).toBeFalse();
  });

  it('hides costs and margins from Sales and Warehouse', async () => {
    for (const role of ['Sales', 'Warehouse']) {
      TestBed.resetTestingModule();
      const service = await serviceFor([role]);
      expect(service.canSeeCosts()).withContext(role).toBeFalse();
      expect(service.canSeeMargins()).withContext(role).toBeFalse();
    }
  });

  it('hides costs when the user has no roles', async () => {
    const service = await serviceFor(undefined);
    expect(service.canSeeCosts()).toBeFalse();
    expect(service.canSeeMargins()).toBeFalse();
  });
});
