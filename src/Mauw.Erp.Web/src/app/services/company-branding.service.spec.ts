import { of, throwError } from 'rxjs';
import { ApiService } from './api.service';
import { CompanyBrandingService } from './company-branding.service';

describe('CompanyBrandingService', () => {
  let api: jasmine.SpyObj<ApiService>;
  let service: CompanyBrandingService;
  const svg = '<svg xmlns="http://www.w3.org/2000/svg" width="200" height="100">' +
    '<rect width="200" height="100" fill="#915f6d"/></svg>';

  beforeEach(() => {
    api = jasmine.createSpyObj<ApiService>('ApiService', ['put']);
    api.put.and.returnValue(of(undefined));
    service = new CompanyBrandingService(api, document.implementation.createHTMLDocument());
  });

  it('normalizes an SVG into PNG pixels and refreshes image addresses after saving', async () => {
    const originalUrl = service.logoUrl('horizontal');
    await service.replaceLogo('horizontal', new File([svg], 'logo.svg', { type: 'image/svg+xml' }));

    const [endpoint, body] = api.put.calls.mostRecent().args as [string, { pngData: string }];
    expect(endpoint).toBe('v1/settings/company/logos/horizontal');
    const bytes = atob(body.pngData);
    expect(Array.from(bytes.slice(0, 8), char => char.charCodeAt(0))).toEqual([137, 80, 78, 71, 13, 10, 26, 10]);
    expect(service.logoUrl('horizontal')).not.toBe(originalUrl);
    expect(service.logoUrl('compact')).toContain('?v=1');
  });

  it('rejects empty, oversized and unsupported uploads without contacting the API', async () => {
    await expectAsync(service.replaceLogo('horizontal', new File([], 'empty.svg', { type: 'image/svg+xml' })))
      .toBeRejectedWithError();
    await expectAsync(service.replaceLogo('horizontal',
      new File([new Uint8Array(2 * 1024 * 1024 + 1)], 'big.png', { type: 'image/png' }))).toBeRejectedWithError();
    await expectAsync(service.replaceLogo('horizontal',
      new File(['text'], 'file.txt', { type: 'text/plain' }))).toBeRejectedWithError();
    expect(api.put).not.toHaveBeenCalled();
  });

  it('rejects SVG references to external resources and active content', async () => {
    const external = '<svg xmlns="http://www.w3.org/2000/svg"><image href="https://example.test/logo.png"/></svg>';
    const active = '<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>';
    for (const content of [external, active, '<svg>broken']) {
      await expectAsync(service.replaceLogo('compact',
        new File([content], 'logo.svg', { type: 'image/svg+xml' }))).toBeRejectedWithError();
    }
    expect(api.put).not.toHaveBeenCalled();
  });

  it('propagates save failures without refreshing the displayed logos', async () => {
    const originalUrl = service.logoUrl('horizontal');
    const error = { error: 'Salvataggio non riuscito' };
    api.put.and.returnValue(throwError(() => error));

    await expectAsync(service.replaceLogo('horizontal', new File([svg], 'logo.svg', { type: 'image/svg+xml' })))
      .toBeRejectedWith(error);
    expect(service.logoUrl('horizontal')).toBe(originalUrl);
  });
});
