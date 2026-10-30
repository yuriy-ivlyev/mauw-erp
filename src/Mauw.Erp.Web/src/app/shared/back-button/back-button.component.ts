import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-back-button',
  standalone: true,
  template: `
    <button type="button" [disabled]="disabled" (click)="activated.emit()"
      class="inline-flex h-9 items-center gap-2 rounded-md border border-blue-200 bg-blue-100 px-3 text-sm font-semibold text-blue-800 shadow-sm hover:bg-blue-200 focus-visible:outline focus-visible:outline-2 focus-visible:outline-blue-600 disabled:opacity-50">
      <svg aria-hidden="true" focusable="false" class="h-4 w-4" viewBox="0 0 24 24"
        fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="m15 18-6-6 6-6" />
      </svg>
      {{ label }}
    </button>
  `
})
export class BackButtonComponent {
  @Input() label = 'Indietro';
  @Input() disabled = false;
  @Output() activated = new EventEmitter<void>();
}
