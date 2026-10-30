import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

export type TableActionKind = 'edit' | 'delete';

@Component({
  selector: 'app-table-action-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button type="button" [disabled]="disabled" [attr.aria-label]="label" [title]="label"
      [ngClass]="action === 'edit'
        ? 'inline-flex h-8 w-8 items-center justify-center rounded-md bg-green-600 text-white hover:bg-green-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-green-700 disabled:opacity-50'
        : 'inline-flex h-8 w-8 items-center justify-center rounded-md bg-red-600 text-white hover:bg-red-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-red-700 disabled:opacity-50'"
      (click)="activated.emit()">
      <svg *ngIf="action === 'edit'" aria-hidden="true" focusable="false" viewBox="0 0 24 24"
        class="h-4 w-4" fill="none" stroke="currentColor" stroke-width="2"
        stroke-linecap="round" stroke-linejoin="round">
        <path d="M12 20h9" />
        <path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L8 18l-4 1 1-4Z" />
      </svg>
      <svg *ngIf="action === 'delete'" aria-hidden="true" focusable="false" viewBox="0 0 24 24"
        class="h-4 w-4" fill="none" stroke="currentColor" stroke-width="2"
        stroke-linecap="round" stroke-linejoin="round">
        <path d="M3 6h18" />
        <path d="M8 6V4h8v2" />
        <path d="m19 6-1 14H6L5 6" />
        <path d="M10 11v5M14 11v5" />
      </svg>
    </button>
  `
})
export class TableActionButtonComponent {
  @Input({ required: true }) action!: TableActionKind;
  @Input({ required: true }) label!: string;
  @Input() disabled = false;
  @Output() activated = new EventEmitter<void>();
}
