import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, takeUntil } from 'rxjs/operators';
import { ContactService } from '../../../services/contact.service';
import { ContactSummary, ContactFilterParams, PagedResult } from '../../../models/contact.model';
import { ConfirmDeleteModalComponent } from '../../../shared/confirm-delete-modal/confirm-delete-modal.component';

@Component({
  selector: 'app-anagrafiche-elenco',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, ConfirmDeleteModalComponent],
  templateUrl: './anagrafiche-elenco.component.html'
})
export class AnagraficheElencoComponent implements OnInit, OnDestroy {
  contacts: ContactSummary[] = [];
  totalCount = 0;
  totalPages = 0;

  loading = false;
  errorMessage = '';
  successMessage = '';

  searchTerm = '';
  roleFilter: 'all' | 'customer' | 'supplier' | 'agent' = 'all';
  statusFilter = '';
  currentPage = 1;
  pageSize = 20;

  confirmDeleteId: string | null = null;

  private searchSubject = new Subject<string>();
  private destroy$ = new Subject<void>();

  constructor(
    private contactService: ContactService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.searchSubject.pipe(
      debounceTime(400),
      distinctUntilChanged(),
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.currentPage = 1;
      this.loadContacts();
    });

    this.loadContacts();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  onSearchChange(): void {
    this.searchSubject.next(this.searchTerm);
  }

  onFilterChange(): void {
    this.currentPage = 1;
    this.loadContacts();
  }

  loadContacts(): void {
    this.loading = true;
    this.errorMessage = '';

    const params: ContactFilterParams = {
      search: this.searchTerm || undefined,
      page: this.currentPage,
      pageSize: this.pageSize,
      status: this.statusFilter || undefined,
      isCustomer: this.roleFilter === 'customer' ? true : undefined,
      isSupplier: this.roleFilter === 'supplier' ? true : undefined,
      isAgent:    this.roleFilter === 'agent'    ? true : undefined
    };

    this.contactService.getList(params).subscribe({
      next: (result: PagedResult<ContactSummary>) => {
        this.contacts   = result.items;
        this.totalCount = result.totalCount;
        this.totalPages = result.totalPages;
        this.loading    = false;
      },
      error: () => {
        this.errorMessage = 'Errore nel caricamento delle anagrafiche.';
        this.loading = false;
      }
    });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage = page;
    this.loadContacts();
  }

  newContact(): void {
    this.router.navigate(['/anagrafiche/nuovo']);
  }

  editContact(id: string): void {
    this.router.navigate(['/anagrafiche', id, 'modifica']);
  }

  askDelete(id: string): void {
    this.confirmDeleteId = id;
  }

  cancelDelete(): void {
    this.confirmDeleteId = null;
  }

  confirmDelete(): void {
    if (!this.confirmDeleteId) return;
    const id = this.confirmDeleteId;
    this.confirmDeleteId = null;

    this.contactService.delete(id).subscribe({
      next: () => {
        this.successMessage = 'Anagrafica eliminata.';
        this.loadContacts();
        setTimeout(() => (this.successMessage = ''), 3000);
      },
      error: () => (this.errorMessage = 'Errore durante l\'eliminazione.')
    });
  }

  pages(): number[] {
    const delta = 2;
    const start = Math.max(1, this.currentPage - delta);
    const end   = Math.min(this.totalPages, this.currentPage + delta);
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  }
}
