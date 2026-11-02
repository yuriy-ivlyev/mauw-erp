import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, of } from 'rxjs';
import { catchError, debounceTime, finalize, switchMap, takeUntil } from 'rxjs/operators';
import { CreditNoteIssued, CreditNotePreview, CreditNoteReason, CreditNoteSelectableLine, CreateCreditNoteRequest } from '../../../models/credit-note.model';
import { Invoice } from '../../../models/invoice.model';
import { WarehouseSummary } from '../../../models/warehouse.model';
import { CreditNoteService } from '../../../services/credit-note.service';
import { PageSlicePipe } from '../../../shared/pagination/page-slice.pipe';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';

type CreditNoteModalLine = CreditNoteSelectableLine & {
  selected: boolean;
  quantity: number;
};

@Component({
  selector: 'app-credit-note-modal',
  standalone: true,
  imports: [CommonModule, FormsModule, PaginationComponent, PageSlicePipe],
  templateUrl: './credit-note-modal.component.html'
})
export class CreditNoteModalComponent implements OnInit, OnDestroy {
  @Input({ required: true }) invoice!: Invoice;
  @Input() warehouses: WarehouseSummary[] = [];
  @Output() completed = new EventEmitter<CreditNoteIssued>();
  @Output() closed = new EventEmitter<void>();

  mode: 'partial' | 'full' = 'partial';
  reason: CreditNoteReason = 'GoodsReturn';
  returnToWarehouse = false;
  warehouseId = '';
  lines: CreditNoteModalLine[] = [];
  preview: CreditNotePreview | null = null;
  previewLoading = false;
  previewError = '';
  submitting = false;
  submitError = '';
  page = 1;
  pageSize = 10;

  readonly reasons: { value: CreditNoteReason; label: string }[] = [
    { value: 'GoodsReturn', label: 'Reso merce' },
    { value: 'PriceAdjustment', label: 'Rettifica prezzo' },
    { value: 'CommercialAllowance', label: 'Abbuono commerciale' },
    { value: 'FullCancellation', label: 'Storno totale' }
  ];

  private readonly previewChanges$ = new Subject<void>();
  private readonly destroy$ = new Subject<void>();

  constructor(private readonly creditNotesApi: CreditNoteService) {}

  ngOnInit(): void {
    this.previewChanges$.pipe(
      debounceTime(200),
      switchMap(() => {
        this.previewLoading = true;
        this.previewError = '';
        return this.creditNotesApi.previewCreditNote(this.buildRequest()).pipe(
          catchError(error => {
            this.previewError = this.describeError(error, 'Impossibile calcolare l’anteprima.');
            return of(null);
          }),
          finalize(() => { this.previewLoading = false; })
        );
      }),
      takeUntil(this.destroy$)
    ).subscribe(result => {
      if (result) {
        this.applyPreview(result);
      }
    });

    this.refreshPreview();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get selectedCount(): number {
    return this.lines.filter(line => line.selected).length;
  }

  get canIssue(): boolean {
    return !this.submitting &&
      !this.previewLoading &&
      !this.previewError &&
      this.selectedCount > 0 &&
      (this.preview?.grossTotal ?? 0) > 0 &&
      this.lines.filter(line => line.selected).every(line =>
        Number.isFinite(line.quantity) &&
        line.quantity >= 1 &&
        line.quantity <= line.remainingQuantity) &&
      (!this.returnToWarehouse || !!this.warehouseId);
  }

  setMode(mode: 'partial' | 'full'): void {
    this.mode = mode;
    if (mode === 'full') {
      this.reason = 'FullCancellation';
      this.lines = this.lines.map(line => ({
        ...line,
        selected: line.remainingQuantity > 0,
        quantity: line.remainingQuantity
      }));
    } else {
      this.reason = 'GoodsReturn';
      this.lines = this.lines.map(line => ({ ...line, selected: false, quantity: 0 }));
    }
    this.refreshPreview();
  }

  onReasonChange(reason: CreditNoteReason): void {
    this.reason = reason;
    this.refreshPreview();
  }

  onReturnToWarehouseChange(enabled: boolean): void {
    this.returnToWarehouse = enabled;
    if (!enabled) {
      this.warehouseId = '';
    }
    this.refreshPreview();
  }

  onLineSelectionChange(line: CreditNoteModalLine, selected: boolean): void {
    if (this.mode === 'full') {
      return;
    }
    line.selected = selected;
    line.quantity = selected ? line.remainingQuantity : 0;
    this.refreshPreview();
  }

  onQuantityChange(line: CreditNoteModalLine, quantity: number): void {
    line.quantity = Number(quantity);
    this.refreshPreview();
  }

  onPageSizeChange(pageSize: number): void {
    this.pageSize = pageSize;
    this.page = 1;
  }

  submit(): void {
    if (!this.canIssue) {
      return;
    }
    this.submitting = true;
    this.submitError = '';
    this.creditNotesApi.createCreditNote(this.buildRequest()).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.submitting = false; })
    ).subscribe({
      next: result => {
        if (result.courtesyPdf) {
          this.downloadCourtesyPdf(result.courtesyPdf, result.creditNote.creditNoteNumber);
        }
        this.completed.emit(result);
      },
      error: error => {
        this.submitError = this.describeError(error, 'Impossibile emettere la nota di credito.');
      }
    });
  }

  close(): void {
    if (!this.submitting) {
      this.closed.emit();
    }
  }

  private buildRequest(): CreateCreditNoteRequest {
    return {
      invoiceId: this.invoice.id,
      reason: this.reason,
      lines: this.lines
        .filter(line => line.selected)
        .map(line => ({
          invoiceItemId: line.invoiceItemId,
          isShippingCharge: line.isShippingCharge,
          quantity: line.quantity
        })),
      returnToWarehouse: this.returnToWarehouse,
      warehouseId: this.returnToWarehouse ? this.warehouseId || null : null
    };
  }

  refreshPreview(): void {
    this.previewLoading = true;
    this.previewError = '';
    this.previewChanges$.next();
  }

  private applyPreview(preview: CreditNotePreview): void {
    const previous = new Map(this.lines.map(line => [this.lineKey(line), line]));
    this.preview = preview;
    this.lines = preview.availableLines.map(source => {
      const current = previous.get(this.lineKey(source));
      const selected = this.mode === 'full'
        ? source.remainingQuantity > 0
        : current?.selected === true && source.remainingQuantity > 0;
      return {
        ...source,
        selected,
        quantity: selected
          ? this.mode === 'full'
            ? source.remainingQuantity
            : Math.min(current?.quantity ?? source.remainingQuantity, source.remainingQuantity)
          : 0
      };
    });
  }

  private lineKey(line: Pick<CreditNoteSelectableLine, 'invoiceItemId' | 'isShippingCharge'>): string {
    return line.isShippingCharge ? 'shipping' : line.invoiceItemId ?? '';
  }

  private downloadCourtesyPdf(base64: string, documentNumber: string): void {
    const binary = window.atob(base64);
    const bytes = Uint8Array.from(binary, character => character.charCodeAt(0));
    const url = URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = `nota-di-credito-${documentNumber}.pdf`;
    link.click();
    URL.revokeObjectURL(url);
  }

  private describeError(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        return error.error.trim();
      }
      if (typeof error.error?.message === 'string' && error.error.message.trim()) {
        return error.error.message.trim();
      }
    }
    return fallback;
  }
}
