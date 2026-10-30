import { TestBed } from '@angular/core/testing';
import { AddActionButtonComponent } from './add-action-button.component';

describe('AddActionButtonComponent', () => {
  it('renders an accessible green plus button and emits activation', async () => {
    await TestBed.configureTestingModule({ imports: [AddActionButtonComponent] }).compileComponents();
    const fixture = TestBed.createComponent(AddActionButtonComponent);
    fixture.componentInstance.label = 'Nuovo ordine';
    fixture.detectChanges();
    const activated = jasmine.createSpy('activated');
    fixture.componentInstance.activated.subscribe(activated);

    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.getAttribute('aria-label')).toBe('Nuovo ordine');
    expect(button.title).toBe('Nuovo ordine');
    expect(button.className).toContain('bg-green-600');
    expect(button.querySelector('svg')).not.toBeNull();
    expect(button.textContent?.trim()).toBe('');
    button.click();

    expect(activated).toHaveBeenCalled();
  });
});
