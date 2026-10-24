import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../../services/auth.service';
import { CompanyBrandingService } from '../../../services/company-branding.service';
import { SidebarComponent } from './sidebar.component';

describe('SidebarComponent', () => {
  async function createSidebar(roles: string[]) {
    await TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { hasRole: (role: string) => roles.includes(role) } },
        { provide: CompanyBrandingService, useValue: { logoUrl: () => '/logo.png' } }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(SidebarComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('opens and closes sections through the rendered menu without resetting other sections', async () => {
    const fixture = await createSidebar(['Admin']);
    const element: HTMLElement = fixture.nativeElement;
    const section = (label: string) => Array.from(element.querySelectorAll<HTMLElement>('nav div'))
      .find(node => node.classList.contains('cursor-pointer') && node.textContent?.includes(label))!;
    const hasLink = (label: string) => Array.from(element.querySelectorAll('nav a'))
      .some(link => link.textContent?.trim() === label);

    expect(hasLink('Prodotti')).toBeFalse();
    section('Catalogo').click();
    fixture.detectChanges();
    expect(hasLink('Prodotti')).toBeTrue();
    section('Sistema').click();
    fixture.detectChanges();
    expect(hasLink('Azienda')).toBeTrue();
    expect(hasLink('Prodotti')).toBeTrue();
    section('Catalogo').click();
    fixture.detectChanges();
    expect(hasLink('Prodotti')).toBeFalse();
    expect(hasLink('Azienda')).toBeTrue();
  });

  it('keeps unauthorized links hidden when opening sections', async () => {
    const fixture = await createSidebar(['Sales']);
    const catalog = fixture.componentInstance.menuItems.find(item => item.label === 'Catalogo')!;
    fixture.componentInstance.toggleSubmenu(catalog);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain('Listini');
    expect(text).not.toContain('Prodotti');
    expect(text).not.toContain('Sistema');
    expect(text).not.toContain('Cassa');
    expect(text).not.toContain('Acquisti');
  });

  it('shows supplier stock-in under Purchases immediately after Sales for warehouse users', async () => {
    const fixture = await createSidebar(['Warehouse']);
    const menu = fixture.componentInstance.menuItems;
    const purchases = menu.find(item => item.label === 'Acquisti')!;
    expect(purchases).toBeTruthy();
    fixture.componentInstance.toggleSubmenu(purchases);
    fixture.detectChanges();
    const link: HTMLAnchorElement | null = fixture.nativeElement.querySelector('a[href="/acquisti/ddt-fornitore"]');
    expect(link?.textContent?.trim()).toBe('Carico Merce');
  });
});
