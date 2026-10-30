import { TestBed } from '@angular/core/testing';
import { TableActionButtonComponent } from './table-action-button.component';

describe('TableActionButtonComponent', () => {
  it('renders an accessible, glyph-only green edit button', async () => {
    await TestBed.configureTestingModule({ imports: [TableActionButtonComponent] }).compileComponents();
    const fixture = TestBed.createComponent(TableActionButtonComponent);
    fixture.componentInstance.action = 'edit';
    fixture.componentInstance.label = 'Modifica riga';
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.getAttribute('aria-label')).toBe('Modifica riga');
    expect(button.className).toContain('bg-green-600');
    expect(button.querySelector('svg')).not.toBeNull();
    expect(button.textContent?.trim()).toBe('');
  });

  it('renders a red delete glyph and emits activation', async () => {
    await TestBed.configureTestingModule({ imports: [TableActionButtonComponent] }).compileComponents();
    const fixture = TestBed.createComponent(TableActionButtonComponent);
    fixture.componentInstance.action = 'delete';
    fixture.componentInstance.label = 'Elimina riga';
    fixture.detectChanges();
    const activated = jasmine.createSpy('activated');
    fixture.componentInstance.activated.subscribe(activated);

    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.className).toContain('bg-red-600');
    button.click();

    expect(activated).toHaveBeenCalled();
  });
});
