import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { SupplierDocumentOcrSettingsService } from '../../../services/supplier-document-ocr-settings.service';
import {
  SupplierDocumentOcrColumn,
  SupplierDocumentOcrHeaderAlias
} from '../../../models/supplier-document-ocr-alias.model';
import { BackButtonComponent } from '../../../shared/back-button/back-button.component';
import { AddActionButtonComponent } from '../../../shared/add-action-button/add-action-button.component';
import { TableActionButtonComponent } from '../../../shared/table-action-button/table-action-button.component';
import { ConfirmDeleteModalComponent } from '../../../shared/confirm-delete-modal/confirm-delete-modal.component';
import { Router } from '@angular/router';

interface OcrAliasColumnDefinition {
  field: SupplierDocumentOcrColumn;
  label: string;
  description: string;
}

@Component({
  selector: 'app-ocr-fornitori',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    BackButtonComponent,
    AddActionButtonComponent,
    TableActionButtonComponent,
    ConfirmDeleteModalComponent
  ],
  templateUrl: './ocr-fornitori.component.html'
})
export class OcrFornitoriComponent implements OnInit {
  private readonly service = inject(SupplierDocumentOcrSettingsService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);

  readonly columns: OcrAliasColumnDefinition[] = [
    { field: 'SupplierCode', label: 'Codice fornitore', description: 'Intestazioni usate per il codice articolo del fornitore.' },
    { field: 'Description', label: 'Descrizione', description: 'Intestazioni usate per la descrizione dell’articolo.' },
    { field: 'Quantity', label: 'Quantità', description: 'Intestazioni usate per la quantità in pezzi.' }
  ];

  aliases: SupplierDocumentOcrHeaderAlias[] = [];
  newAliases: Record<SupplierDocumentOcrColumn, string> = {
    SupplierCode: '',
    Description: '',
    Quantity: ''
  };
  loading = true;
  busy = false;
  error = '';
  success = '';
  deleteConfirmationOpen = false;
  pendingDelete: SupplierDocumentOcrHeaderAlias | null = null;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = '';
    this.service.list()
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading = false))
      .subscribe({
        next: aliases => this.aliases = aliases,
        error: error => this.showError(error, 'Impossibile caricare gli alias OCR.')
      });
  }

  aliasesFor(column: SupplierDocumentOcrColumn): SupplierDocumentOcrHeaderAlias[] {
    return this.aliases.filter(alias => alias.column === column);
  }

  add(column: SupplierDocumentOcrColumn): void {
    const alias = this.newAliases[column].trim();
    if (!alias) {
      this.error = 'Inserire un alias prima di aggiungerlo.';
      return;
    }

    this.error = '';
    this.success = '';
    this.busy = true;
    this.service.add({ column, alias })
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false))
      .subscribe({
        next: created => {
          this.aliases = [...this.aliases, created];
          this.newAliases[column] = '';
          this.success = `Alias "${created.alias}" aggiunto a ${this.labelFor(column)}.`;
        },
        error: error => this.showError(error, 'Impossibile aggiungere l’alias.')
      });
  }

  requestDelete(alias: SupplierDocumentOcrHeaderAlias): void {
    this.pendingDelete = alias;
    this.deleteConfirmationOpen = true;
  }

  confirmDelete(): void {
    if (!this.pendingDelete) return;
    const alias = this.pendingDelete;
    this.error = '';
    this.success = '';
    this.busy = true;
    this.service.delete(alias.id)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false))
      .subscribe({
        next: () => {
          this.aliases = this.aliases.filter(item => item.id !== alias.id);
          this.success = `Alias "${alias.alias}" rimosso.`;
          this.cancelDelete();
        },
        error: error => this.showError(error, 'Impossibile rimuovere l’alias.')
      });
  }

  cancelDelete(): void {
    this.deleteConfirmationOpen = false;
    this.pendingDelete = null;
  }

  goBack(): void {
    void this.router.navigate(['/dashboard']);
  }

  private labelFor(column: SupplierDocumentOcrColumn): string {
    return this.columns.find(item => item.field === column)?.label ?? column;
  }

  private showError(error: unknown, fallback: string): void {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        this.error = error.error;
        return;
      }
      if (typeof error.error?.detail === 'string' && error.error.detail.trim()) {
        this.error = error.error.detail;
        return;
      }
    }

    this.error = fallback;
    this.success = '';
  }
}
