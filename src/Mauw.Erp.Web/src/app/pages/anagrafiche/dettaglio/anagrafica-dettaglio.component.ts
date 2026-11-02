import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { ContactDetail } from '../../../models/contact.model';
import { CustomerStatementDto, StatementMovementDto } from '../../../models/customer-statement.models';
import { ContactService } from '../../../services/contact.service';
import { BackButtonComponent } from '../../../shared/back-button/back-button.component';
import { PageSlicePipe } from '../../../shared/pagination/page-slice.pipe';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';

type ContactDetailTab = 'details' | 'statement';

@Component({
  selector: 'app-anagrafica-dettaglio',
  standalone: true,
  imports: [CommonModule, RouterLink, BackButtonComponent, PaginationComponent, PageSlicePipe],
  templateUrl: './anagrafica-dettaglio.component.html'
})
export class AnagraficaDettaglioComponent implements OnInit, OnDestroy {
  contact: ContactDetail | null = null;
  statement: CustomerStatementDto | null = null;
  activeTab: ContactDetailTab = 'statement';
  loadingContact = true;
  loadingStatement = false;
  errorMessage = '';
  statementError = '';
  currentPage = 1;
  pageSize = 20;

  private readonly destroy$ = new Subject<void>();
  private contactRequestVersion = 0;
  private statementRequestVersion = 0;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly contactsApi: ContactService
  ) {}

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const id = params.get('id');
      if (!id) {
        this.loadingContact = false;
        this.errorMessage = 'Identificativo anagrafica non valido.';
        return;
      }

      this.loadContact(id);
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  goBack(): void {
    void this.router.navigate(['/anagrafiche/elenco']);
  }

  editContact(): void {
    if (this.contact) {
      void this.router.navigate(['/anagrafiche', this.contact.id, 'modifica']);
    }
  }

  selectTab(tab: ContactDetailTab): void {
    this.activeTab = tab;
    if (tab === 'statement' && this.contact?.customerProfile && !this.statement && !this.loadingStatement) {
      this.loadStatement(this.contact.id);
    }
  }

  onPageChange(page: number): void {
    const totalPages = Math.max(1, Math.ceil((this.statement?.movements.length ?? 0) / this.pageSize));
    if (page >= 1 && page <= totalPages) {
      this.currentPage = page;
    }
  }

  onPageSizeChange(pageSize: number): void {
    this.pageSize = pageSize;
    this.currentPage = 1;
  }

  movementTypeLabel(movement: StatementMovementDto): string {
    if (movement.type === 'Invoice') {
      return 'Fattura';
    }
    if (movement.type === 'CreditNote') {
      return 'Nota di credito';
    }
    return this.isCreditNoteOffset(movement) ? 'Compensazione NC' : 'Incasso';
  }

  movementTypeClass(movement: StatementMovementDto): string {
    if (movement.type === 'Invoice') {
      return 'bg-blue-100 text-blue-800';
    }
    if (movement.type === 'CreditNote') {
      return 'bg-amber-100 text-amber-800';
    }
    return this.isCreditNoteOffset(movement)
      ? 'bg-violet-100 text-violet-800'
      : 'bg-green-100 text-green-800';
  }

  isCreditNoteOffset(movement: StatementMovementDto): boolean {
    return movement.type === 'Payment' && movement.paymentMethod === 'CreditNoteOffset';
  }

  private loadContact(id: string): void {
    const requestVersion = ++this.contactRequestVersion;
    this.statementRequestVersion++;
    this.contact = null;
    this.statement = null;
    this.loadingContact = true;
    this.loadingStatement = false;
    this.errorMessage = '';
    this.statementError = '';
    this.currentPage = 1;

    this.contactsApi.getById(id).pipe(
      takeUntil(this.destroy$),
      finalize(() => {
        if (requestVersion === this.contactRequestVersion) {
          this.loadingContact = false;
        }
      })
    ).subscribe({
      next: contact => {
        if (requestVersion !== this.contactRequestVersion) {
          return;
        }
        this.contact = contact;
        if (contact.customerProfile) {
          this.loadStatement(contact.id);
        } else {
          this.activeTab = 'details';
        }
      },
      error: error => {
        if (requestVersion === this.contactRequestVersion) {
          this.errorMessage = this.describeError(error, 'Impossibile caricare l’anagrafica.');
        }
      }
    });
  }

  private loadStatement(customerId: string): void {
    const requestVersion = ++this.statementRequestVersion;
    this.loadingStatement = true;
    this.statementError = '';

    this.contactsApi.getCustomerStatement(customerId).pipe(
      takeUntil(this.destroy$),
      finalize(() => {
        if (requestVersion === this.statementRequestVersion) {
          this.loadingStatement = false;
        }
      })
    ).subscribe({
      next: statement => {
        if (requestVersion === this.statementRequestVersion) {
          this.statement = statement;
          this.currentPage = 1;
        }
      },
      error: error => {
        if (requestVersion === this.statementRequestVersion) {
          this.statementError = this.describeError(error, 'Impossibile caricare l’estratto conto.');
        }
      }
    });
  }

  private describeError(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        return error.error.trim();
      }
      if (typeof error.error?.detail === 'string') {
        return error.error.detail;
      }
    }
    return fallback;
  }
}
