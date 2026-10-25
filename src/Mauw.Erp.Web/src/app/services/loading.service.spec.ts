import { fakeAsync, tick } from '@angular/core/testing';
import { LoadingService } from './loading.service';

describe('LoadingService', () => {
  it('shows the loading state only for requests lasting at least 250 ms', fakeAsync(() => {
    const service = new LoadingService();

    service.beginRequest();
    tick(249);
    expect(service.isLoading()).toBeFalse();

    tick(1);
    expect(service.isLoading()).toBeTrue();

    service.endRequest();
    expect(service.isLoading()).toBeFalse();
  }));

  it('stays active until all concurrent requests complete', fakeAsync(() => {
    const service = new LoadingService();

    service.beginRequest();
    service.beginRequest();
    tick(250);
    expect(service.isLoading()).toBeTrue();

    service.endRequest();
    expect(service.isLoading()).toBeTrue();

    service.endRequest();
    expect(service.isLoading()).toBeFalse();
  }));

  it('does not show when a request finishes before the delay', fakeAsync(() => {
    const service = new LoadingService();

    service.beginRequest();
    tick(100);
    service.endRequest();
    tick(250);

    expect(service.isLoading()).toBeFalse();
  }));
});
