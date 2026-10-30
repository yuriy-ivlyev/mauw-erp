import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ContactService } from '../../services/contact.service';
import { SupplierDeliveryNoteService } from '../../services/supplier-delivery-note.service';
import { ContactSummary } from '../../models/contact.model';
import { SupplierDeliveryNoteListItem } from '../../models/supplier-delivery-note.model';
import { PaginationComponent } from '../../shared/pagination/pagination.component';
import { TableActionButtonComponent } from '../../shared/table-action-button/table-action-button.component';
import { AddActionButtonComponent } from '../../shared/add-action-button/add-action-button.component';

@Component({
  selector: 'app-carico-merce',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink, PaginationComponent,
    TableActionButtonComponent, AddActionButtonComponent
  ],
  templateUrl: './carico-merce.component.html',
  styles: [`
    :host { display: block; }
    input, select { border: 1px solid #d1d5db; border-radius: .4rem; padding: .55rem .7rem; width: 100%; color: #111827; background: white; }
    th, td { padding: .85rem 1rem; text-align: left; vertical-align: middle; }
    .action { padding: .55rem .9rem; border: 1px solid #d1d5db; border-radius: .4rem; }
    .primary { background: #465fff; color: white; border-color: #465fff; }
    .badge { display: inline-flex; border-radius: 9999px; padding: .2rem .65rem; font-size: .75rem; font-weight: 600; }
  `]
})
export class CaricoMerceComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  notes: SupplierDeliveryNoteListItem[] = [];
  suppliers: ContactSummary[] = [];
  page = 1;
  pageSize = 20;
  totalCount = 0;
  totalPages = 0;
  busy = false;
  error = '';
  search = '';
  supplierId = '';
  status = '';
  dateFrom = '';
  dateTo = '';

  readonly labels: Record<string, string> = {
    Draft: 'Bozza', Parsing: 'In elaborazione', Parsed: 'Elaborata',
    Confirmed: 'Confermato', Cancelled: 'Annullato'
  };

  constructor(
    private readonly service: SupplierDeliveryNoteService,
    private readonly contacts: ContactService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.loadSuppliers();
    this.load();
  }

  loadSuppliers(): void {
    this.contacts.getList({ isSupplier: true, status: 'Active', page: 1, pageSize: 100 })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: result => this.suppliers = result.items,
        error: error => this.showError(error)
      });
  }

  applyFilters(): void {
    this.page = 1;
    this.load();
  }

  resetFilters(): void {
    this.search = '';
    this.supplierId = '';
    this.status = '';
    this.dateFrom = '';
    this.dateTo = '';
    this.applyFilters();
  }

  load(): void {
    this.busy = true;
    this.service.list(this.page, {
      search: this.search.trim() || undefined,
      supplierId: this.supplierId || undefined,
      status: this.status || undefined,
      dateFrom: this.dateFrom || undefined,
      dateTo: this.dateTo || undefined
    }, this.pageSize).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: result => {
        this.notes = result.items;
        this.totalCount = result.totalCount;
        this.totalPages = result.totalPages;
        this.busy = false;
      },
      error: error => { this.busy = false; this.showError(error); }
    });
  }

  badgeClass(status: string): string {
    switch (status) {
      case 'Confirmed': return 'bg-green-100 text-green-800';
      case 'Cancelled': return 'bg-gray-100 text-gray-600';
      case 'Parsed': return 'bg-blue-100 text-blue-800';
      case 'Parsing': return 'bg-amber-100 text-amber-800';
      default: return 'bg-yellow-100 text-yellow-800';
    }
  }

  changePage(page: number): void {
    this.page = page;
    this.load();
  }

  changePageSize(pageSize: number): void {
    this.pageSize = pageSize;
    this.page = 1;
    this.load();
  }

  openNote(id: string): void {
    void this.router.navigate(['/acquisti/ddt-fornitore', id]);
  }

  openNewLoad(): void {
    void this.router.navigate(['/acquisti/ddt-fornitore/nuovo']);
  }

  private showError(error: unknown): void {
    if (error instanceof HttpErrorResponse && error.error && typeof error.error.detail === 'string') {
      this.error = error.error.detail;
    } else {
      this.error = 'Impossibile caricare l’elenco. Verificare connessione e autorizzazioni.';
    }
  }
}
