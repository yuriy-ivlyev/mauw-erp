import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, Input, OnChanges, OnDestroy, SimpleChanges } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { CustomerStatementDto, StatementMovementDto } from '../../models/customer-statement.models';
import { ContactService } from '../../services/contact.service';
import { PageSlicePipe } from '../../shared/pagination/page-slice.pipe';
import { PaginationComponent } from '../../shared/pagination/pagination.component';

@Component({
  selector: 'app-customer-statement',
  standalone: true,
  imports: [CommonModule, RouterLink, PaginationComponent, PageSlicePipe],
  templateUrl: './customer-statement.component.html'
})
export class CustomerStatementComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) customerId!: string;

  statement: CustomerStatementDto | null = null;
  loading = false;
  errorMessage = '';
  currentPage = 1;
  pageSize = 20;

  private readonly destroy$ = new Subject<void>();
  private requestVersion = 0;

  constructor(private readonly contactsApi: ContactService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['customerId'] && this.customerId) {
      this.loadStatement();
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadStatement(): void {
    const requestVersion = ++this.requestVersion;
    this.statement = null;
    this.loading = true;
    this.errorMessage = '';
    this.currentPage = 1;

    this.contactsApi.getCustomerStatement(this.customerId).pipe(
      takeUntil(this.destroy$),
      finalize(() => {
        if (requestVersion === this.requestVersion) {
          this.loading = false;
        }
      })
    ).subscribe({
      next: statement => {
        if (requestVersion === this.requestVersion) {
          this.statement = statement;
        }
      },
      error: error => {
        if (requestVersion === this.requestVersion) {
          this.errorMessage = this.describeError(error, 'Impossibile caricare l’estratto conto.');
        }
      }
    });
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
