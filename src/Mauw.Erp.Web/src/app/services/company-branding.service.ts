import { DOCUMENT } from '@angular/common';
import { Inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { ApiService } from './api.service';

export type CompanyLogoKind = 'horizontal' | 'compact';

@Injectable({ providedIn: 'root' })
export class CompanyBrandingService {
  private readonly revision = signal(0);

  constructor(private api: ApiService, @Inject(DOCUMENT) private document: Document) {}

  logoUrl(kind: CompanyLogoKind): string {
    return `${environment.apiUrl}/v1/settings/company/logos/${kind}?v=${this.revision()}`;
  }

  async replaceLogo(kind: CompanyLogoKind, file: File): Promise<void> {
    if (file.size === 0 || file.size > 2 * 1024 * 1024) {
      throw new Error('Seleziona un logo SVG o PNG non vuoto di massimo 2 MiB.');
    }
    const svg = file.type === 'image/svg+xml' || file.name.toLowerCase().endsWith('.svg');
    if (!svg && file.type !== 'image/png') {
      throw new Error('Sono accettati soltanto file SVG o PNG.');
    }
    if (svg) {
      const source = await file.text();
      const parsed = new DOMParser().parseFromString(source, 'image/svg+xml');
      if (parsed.querySelector('parsererror') || parsed.documentElement.localName !== 'svg' ||
          /<!DOCTYPE|<!ENTITY/i.test(source) || parsed.querySelector('script, foreignObject')) {
        throw new Error('SVG non valido o contenente elementi non supportati.');
      }
      for (const element of Array.from(parsed.querySelectorAll('*'))) {
        if (Array.from(element.attributes).some(attribute =>
          attribute.localName === 'href' && !attribute.value.startsWith('#'))) {
          throw new Error('Il logo SVG deve essere autonomo, senza immagini o riferimenti esterni.');
        }
      }
    }

    // The database only receives raster pixels, never executable SVG markup.
    const url = URL.createObjectURL(file);
    try {
      const image = await new Promise<HTMLImageElement>((resolve, reject) => {
        const image = new Image();
        image.onload = () => resolve(image);
        image.onerror = () => reject(new Error('Impossibile leggere il logo selezionato.'));
        image.src = url;
      });
      if (!image.naturalWidth || !image.naturalHeight) {
        throw new Error('Il logo non ha dimensioni valide.');
      }
      const scale = 1600 / Math.max(image.naturalWidth, image.naturalHeight);
      const canvas = this.document.createElement('canvas');
      canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
      canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
      const context = canvas.getContext('2d');
      if (!context) throw new Error('Conversione immagini non disponibile nel browser.');
      context.drawImage(image, 0, 0, canvas.width, canvas.height);
      const pngData = canvas.toDataURL('image/png').split(',')[1];
      await firstValueFrom(this.api.put<void>(`v1/settings/company/logos/${kind}`, { pngData }));
      this.revision.update(value => value + 1);
      const favicon = this.document.querySelector<HTMLLinkElement>('link[rel="icon"]');
      if (favicon) favicon.href = this.logoUrl('compact');
    } finally {
      URL.revokeObjectURL(url);
    }
  }
}
