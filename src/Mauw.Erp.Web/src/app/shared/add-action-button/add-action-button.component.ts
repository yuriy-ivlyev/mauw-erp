import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-add-action-button',
  standalone: true,
  template: `
    <button type="button" [disabled]="disabled" [attr.aria-label]="label" [title]="label"
      class="inline-flex h-8 w-8 items-center justify-center rounded-md bg-green-600 text-white transition hover:bg-green-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-green-700 disabled:opacity-50"
      (click)="activated.emit()">
      <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" class="h-4 w-4"
        fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round">
        <path d="M12 5v14M5 12h14" />
      </svg>
    </button>
  `,
  styles: [':host { display: inline-flex; }']
})
export class AddActionButtonComponent {
  @Input({ required: true }) label!: string;
  @Input() disabled = false;
  @Output() activated = new EventEmitter<void>();
}
