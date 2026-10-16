import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * REGOLA: ogni azione di eliminazione DEVE passare da questo modale.
 * Non usare mai confirm() del browser.
 */
@Component({
  selector: 'app-confirm-delete-modal',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './confirm-delete-modal.component.html'
})
export class ConfirmDeleteModalComponent {
  @Input() isOpen = false;
  @Input() title = 'Conferma eliminazione';
  @Input() message = 'Sei sicuro di voler eliminare questo elemento? L\'operazione non è reversibile.';
  @Output() confirmed = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();
}
