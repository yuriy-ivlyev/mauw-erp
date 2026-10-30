import { TestBed } from '@angular/core/testing';
import { BackButtonComponent } from './back-button.component';

describe('BackButtonComponent', () => {
  it('renders a light-blue button and emits activation', async () => {
    await TestBed.configureTestingModule({ imports: [BackButtonComponent] }).compileComponents();
    const fixture = TestBed.createComponent(BackButtonComponent);
    fixture.componentInstance.label = 'Torna all’elenco';
    fixture.detectChanges();
    const activated = jasmine.createSpy('activated');
    fixture.componentInstance.activated.subscribe(activated);

    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button).not.toBeNull();
    expect(fixture.nativeElement.querySelector('a')).toBeNull();
    expect(button.className).toContain('bg-blue-100');
    expect(button.textContent).toContain('Torna all’elenco');
    button.click();

    expect(activated).toHaveBeenCalled();
  });
});
