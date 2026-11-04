import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import {
  CreatePayableDocumentRequest,
  PayableDocument,
  PayableDocumentKind,
  PayableDocumentLineRequest,
  PayableDocumentListItem,
  PayableExpenseCategory
} from '../../models/payable-document.model';
import { ContactSummary } from '../../models/contact.model';
import { Employee } from '../../models/employee.model';
import { AuthService } from '../../services/auth.service';
import { ContactService } from '../../services/contact.service';
import { EmployeeService } from '../../services/employee.service';
import { PayableDocumentService } from '../../services/payable-document.service';
import { AddActionButtonComponent } from '../../shared/add-action-button/add-action-button.component';
import { BackButtonComponent } from '../../shared/back-button/back-button.component';
import { PaginationComponent } from '../../shared/pagination/pagination.component';
import { ConfirmDeleteModalComponent } from '../../shared/confirm-delete-modal/confirm-delete-modal.component';

function toLocalDateInput(value: Date): string {
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${value.getFullYear()}-${month}-${day}`;
}

@Component({
  selector: 'app-payable-documents',
  standalone: true,
  imports: [
    CommonModule, FormsModule, AddActionButtonComponent, BackButtonComponent,
    PaginationComponent, ConfirmDeleteModalComponent
  ],
  templateUrl: './payable-documents.component.html'
})
export class PayableDocumentsComponent implements OnInit, OnDestroy {
  readonly categories: Array<{ value: PayableExpenseCategory; label: string }> = [
    { value: 'Merchandise', label: 'Merce' },
    { value: 'Utilities', label: 'Utenze' },
    { value: 'Services', label: 'Servizi' },
    { value: 'MaintenanceWork', label: 'Manutenzioni / lavori' },
    { value: 'Travel', label: 'Trasferte' },
    { value: 'ProfessionalFees', label: 'Compensi professionali' },
    { value: 'AssetCandidate', label: 'Possibile cespite (da verificare)' },
    { value: 'Other', label: 'Altro' }
  ];
  readonly natureCodes = [
    'N1', 'N2.1', 'N2.2', 'N3.1', 'N3.2', 'N3.3', 'N3.4', 'N3.5', 'N3.6',
    'N4', 'N5', 'N6.1', 'N6.2', 'N6.3', 'N6.4', 'N6.5', 'N6.6', 'N6.7',
    'N6.8', 'N6.9', 'N7'
  ];
  readonly paymentMethods = ['Bonifico bancario', 'Carta', 'Contanti', 'Assegno', 'Altro'];
  pageSize = 20;

  documents: PayableDocumentListItem[] = [];
  suppliers: ContactSummary[] = [];
  employees: Employee[] = [];
  selected: PayableDocument | null = null;
  loading = false;
  saving = false;
  uploading = false;
  submitting = false;
  approving = false;
  recordingPayment = false;
  errorMessage = '';
  successMessage = '';
  currentPage = 1;
  totalCount = 0;
  reimbursementsOnly = false;
  editorMode = false;
  canManageAll = false;
  search = '';
  filterKind = '';
  filterStatus = '';
  dateFrom = '';
  dateTo = '';

  editing = false;
  kind: PayableDocumentKind = 'SupplierInvoice';
  supplierId = '';
  employeeId = '';
  sourceDocumentNumber = '';
  documentDate = '';
  dueDate = '';
  notes = '';
  draftLines: PayableDocumentLineRequest[] = [this.newLine()];
  selectedFile: File | null = null;
  paymentAmount: number | null = null;
  paymentDate = toLocalDateInput(new Date());
  paymentMethod = 'Bonifico bancario';
  transactionDetails = '';
  rejectionReason = '';
  private returnUrl = '/acquisti/passivi';
  cancelConfirmationOpen = false;

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly documentsApi: PayableDocumentService,
    private readonly contactsApi: ContactService,
    private readonly employeesApi: EmployeeService,
    private readonly auth: AuthService
  ) {}

  ngOnInit(): void {
    this.canManageAll = this.auth.hasRole('Admin')
      || this.auth.hasRole('Accounting');
    this.reimbursementsOnly = this.route.snapshot.data['reimbursementsOnly'] === true;
    this.editorMode = this.route.snapshot.data['editor'] === true;
    const listRoute = this.reimbursementsOnly ? '/acquisti/rimborsi' : '/acquisti/passivi';
    const requestedReturn = this.route.snapshot.queryParamMap.get('returnTo');
    this.returnUrl = requestedReturn
      && (requestedReturn === listRoute || requestedReturn.startsWith(`${listRoute}?`))
      ? requestedReturn
      : listRoute;

    if (this.canManageAll && !this.reimbursementsOnly) {
      this.loadSuppliers();
    }
    if (this.reimbursementsOnly && this.auth.hasRole('Admin')) {
      this.loadEmployees();
    }
    if (this.editorMode) {
      const documentId = this.route.snapshot.paramMap.get('id');
      if (documentId) {
        this.loadDocument(documentId);
      } else {
        this.initializeNewDraft(this.reimbursementsOnly ? 'ExpenseReimbursement' : 'SupplierInvoice');
      }
      return;
    }

    this.restoreListState();
    this.load();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  goBack(): void {
    void this.router.navigateByUrl(this.editorMode
      ? this.returnUrl
      : '/');
  }

  applyFilters(): void {
    this.currentPage = 1;
    this.load();
  }

  changePage(page: number): void {
    this.currentPage = page;
    this.load();
  }

  changePageSize(pageSize: number): void {
    this.currentPage = 1;
    this.pageSize = pageSize;
    this.load(pageSize);
  }

  load(pageSize = this.pageSize): void {
    this.loading = true;
    this.errorMessage = '';
    const request = this.reimbursementsOnly
      ? this.documentsApi.listReimbursements(this.currentPage, pageSize)
      : this.documentsApi.list(this.currentPage, pageSize, {
        search: this.search.trim() || undefined,
        kind: this.filterKind || undefined,
        status: this.filterStatus || undefined,
        dateFrom: this.dateFrom || undefined,
        dateTo: this.dateTo || undefined
      });

    request.pipe(takeUntil(this.destroy$), finalize(() => { this.loading = false; }))
      .subscribe({
        next: result => {
          this.documents = result.items;
          this.totalCount = result.totalCount;
        },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile caricare i documenti passivi.'); }
      });
  }

  loadSuppliers(): void {
    this.contactsApi.getList({ isSupplier: true, status: 'Active', page: 1, pageSize: 100 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => { this.suppliers = result.items; },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile caricare i fornitori.'); }
      });
  }

  loadEmployees(): void {
    this.employeesApi.list(1, 100, '', true)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: result => { this.employees = result.items; },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile caricare i dipendenti.'); }
      });
  }

  startNew(kind: PayableDocumentKind): void {
    const route = kind === 'ExpenseReimbursement' ? '/acquisti/rimborsi' : '/acquisti/passivi';
    void this.router.navigate([route, 'nuovo'], {
      queryParams: { returnTo: this.listReturnUrl }
    });
  }

  private initializeNewDraft(kind: PayableDocumentKind): void {
    this.selected = null;
    this.kind = kind;
    this.supplierId = '';
    this.employeeId = '';
    this.sourceDocumentNumber = '';
    this.documentDate = toLocalDateInput(new Date());
    this.dueDate = this.documentDate;
    this.notes = '';
    this.draftLines = [this.newLine()];
    this.selectedFile = null;
    this.editing = true;
    this.errorMessage = '';
    this.successMessage = '';
  }

  openDocument(item: PayableDocumentListItem): void {
    const route = this.reimbursementsOnly ? '/acquisti/rimborsi' : '/acquisti/passivi';
    void this.router.navigate([route, item.id], {
      queryParams: { returnTo: this.listReturnUrl }
    });
  }

  private loadDocument(id: string): void {
    this.errorMessage = '';
    const request = this.reimbursementsOnly
      ? this.documentsApi.getReimbursement(id)
      : this.documentsApi.get(id);
    request.pipe(takeUntil(this.destroy$)).subscribe({
      next: document => {
        this.selected = document;
        this.editing = document.status === 'Draft' || document.status === 'Rejected';
        this.copyToDraft(document);
        this.paymentAmount = document.balanceDue;
        this.successMessage = '';
      },
      error: error => { this.errorMessage = this.describeError(error, 'Impossibile aprire il documento.'); }
    });
  }

  saveDraft(): void {
    if (this.saving || !this.validDraft()) {
      return;
    }
    const request = this.toRequest();
    this.saving = true;
    this.errorMessage = '';
    const save = this.selected
      ? this.documentsApi.update(this.selected.id, request, this.reimbursementsOnly)
      : this.reimbursementsOnly
        ? this.documentsApi.createReimbursement(request)
        : this.documentsApi.createSupplierInvoice(request);

    save.pipe(takeUntil(this.destroy$), finalize(() => { this.saving = false; }))
      .subscribe({
        next: document => {
          this.selected = document;
          this.editing = true;
          this.copyToDraft(document);
          this.successMessage = 'Bozza salvata. Allegare il documento originale prima dell’invio.';
          if (!this.editorMode) {
            this.load();
          }
        },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile salvare la bozza.'); }
      });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
  }

  uploadAttachment(): void {
    const document = this.selected;
    const file = this.selectedFile;
    if (!document || !file || document.status !== 'Draft' || this.uploading) {
      return;
    }
    this.uploading = true;
    this.errorMessage = '';
    this.documentsApi.uploadAttachment(
      document.id, document.version, file, this.reimbursementsOnly
    ).pipe(takeUntil(this.destroy$), finalize(() => { this.uploading = false; }))
      .subscribe({
        next: updated => {
          this.selected = updated;
          this.selectedFile = null;
          this.successMessage = 'Allegato caricato.';
          if (!this.editorMode) {
            this.load();
          }
        },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile caricare l’allegato.'); }
      });
  }

  submitForApproval(): void {
    const document = this.selected;
    if (!document || !document.hasAttachment || document.status !== 'Draft' || this.submitting) {
      return;
    }
    this.submitting = true;
    this.errorMessage = '';
    this.documentsApi.submit(document.id, document.version, this.reimbursementsOnly)
      .pipe(takeUntil(this.destroy$), finalize(() => { this.submitting = false; }))
      .subscribe({
        next: updated => {
          this.selected = updated;
          this.editing = false;
          this.successMessage = 'Documento inviato in approvazione.';
          if (!this.editorMode) {
            this.load();
          }
        },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile inviare il documento in approvazione.'); }
      });
  }

  approve(): void {
    const document = this.selected;
    if (!document || document.status !== 'PendingApproval' || this.approving) {
      return;
    }
    this.approving = true;
    this.errorMessage = '';
    this.documentsApi.approve(document.id, document.version)
      .pipe(takeUntil(this.destroy$), finalize(() => { this.approving = false; }))
      .subscribe({
        next: updated => {
          this.selected = updated;
          this.successMessage = 'Documento approvato.';
          if (!this.editorMode) {
            this.load();
          }
        },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile approvare il documento.'); }
      });
  }

  reject(): void {
    const document = this.selected;
    const reason = this.rejectionReason.trim();
    if (!document || document.status !== 'PendingApproval' || !reason) {
      this.errorMessage = 'Inserire il motivo del rifiuto.';
      return;
    }
    this.errorMessage = '';
    this.documentsApi.reject(document.id, document.version, reason)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: updated => {
          this.selected = updated;
          this.rejectionReason = '';
          this.successMessage = 'Documento respinto.';
          if (!this.editorMode) {
            this.load();
          }
        },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile respingere il documento.'); }
      });
  }

  cancelDraft(): void {
    const document = this.selected;
    if (!document || document.status === 'Approved' || document.status === 'Cancelled') {
      return;
    }
    this.cancelConfirmationOpen = true;
  }

  confirmCancelDraft(): void {
    const document = this.selected;
    if (!document || !this.cancelConfirmationOpen) {
      return;
    }
    this.documentsApi.cancel(document.id, document.version, this.reimbursementsOnly)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: updated => {
          this.selected = updated;
          this.editing = false;
          this.cancelConfirmationOpen = false;
          this.successMessage = 'Documento annullato.';
          if (!this.editorMode) {
            this.load();
          }
        },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile annullare il documento.'); }
      });
  }

  closeCancelConfirmation(): void {
    this.cancelConfirmationOpen = false;
  }

  recordPayment(): void {
    const document = this.selected;
    const amount = this.paymentAmount ?? 0;
    if (!document || document.status !== 'Approved' || amount <= 0 || !this.paymentMethod.trim()) {
      this.errorMessage = 'Inserire un importo e un metodo di pagamento validi.';
      return;
    }
    this.recordingPayment = true;
    this.errorMessage = '';
    this.documentsApi.recordPayment(document.id, {
      version: document.version,
      amount,
      paymentDate: this.paymentDate ? `${this.paymentDate}T00:00:00Z` : null,
      paymentMethod: this.paymentMethod.trim(),
      transactionDetails: this.transactionDetails.trim() || null
    }).pipe(takeUntil(this.destroy$), finalize(() => { this.recordingPayment = false; }))
      .subscribe({
        next: updated => {
          this.selected = updated;
          this.paymentAmount = updated.balanceDue;
          this.transactionDetails = '';
          this.successMessage = 'Pagamento registrato.';
          if (!this.editorMode) {
            this.load();
          }
        },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile registrare il pagamento.'); }
      });
  }

  addLine(): void {
    this.draftLines = [...this.draftLines, this.newLine()];
  }

  removeLine(index: number): void {
    if (this.draftLines.length > 1) {
      this.draftLines = this.draftLines.filter((_, current) => current !== index);
    }
  }

  categoryLabel(value: string): string {
    return this.categories.find(category => category.value === value)?.label ?? value;
  }

  kindLabel(kind: string): string {
    if (kind === 'ExpenseReimbursement') return 'Rimborso spese';
    if (kind === 'EmployeePayroll') return 'Costo mensile dipendente';
    return 'Fattura fornitore';
  }

  statusLabel(status: string): string {
    const labels: Record<string, string> = {
      Draft: 'Bozza',
      PendingApproval: 'In approvazione',
      Approved: 'Approvato',
      Rejected: 'Respinto',
      Cancelled: 'Annullato'
    };
    return labels[status] ?? status;
  }

  statusClass(status: string): string {
    const classes: Record<string, string> = {
      Draft: 'bg-slate-100 text-slate-700',
      PendingApproval: 'bg-amber-100 text-amber-800',
      Approved: 'bg-blue-100 text-blue-800',
      Rejected: 'bg-red-100 text-red-800',
      Cancelled: 'bg-gray-100 text-gray-600'
    };
    return classes[status] ?? 'bg-slate-100 text-slate-700';
  }

  paymentStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      Unpaid: 'Da pagare',
      PartiallyPaid: 'Parziale',
      Paid: 'Pagato',
      NotPayable: 'Non liquidabile'
    };
    return labels[status] ?? status;
  }

  downloadAttachment(document: PayableDocument): void {
    this.documentsApi.downloadAttachment(document.id, this.reimbursementsOnly)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: blob => {
          const url = URL.createObjectURL(blob);
          const link = window.document.createElement('a');
          link.href = url;
          link.download = document.attachmentFileName ?? `${document.documentCode}.pdf`;
          link.click();
          window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
        },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile scaricare l’allegato.'); }
      });
  }

  private validDraft(): boolean {
    if (!this.documentDate || !this.dueDate) {
      this.errorMessage = 'Inserire data documento e scadenza.';
      return false;
    }
    if (this.kind === 'SupplierInvoice' && (!this.supplierId || !this.sourceDocumentNumber.trim())) {
      this.errorMessage = 'Selezionare il fornitore e inserire il numero fattura.';
      return false;
    }
    if (this.kind === 'ExpenseReimbursement' && !this.employeeId) {
      this.errorMessage = 'Selezionare il dipendente beneficiario del rimborso.';
      return false;
    }
    if (this.draftLines.length === 0 || this.draftLines.some(line =>
      !line.description.trim() || line.taxableAmount < 0 || line.vatAmount < 0
      || line.grossAmount <= 0 || this.kind === 'SupplierInvoice' && line.vatRate === null
      || line.vatRate === 0 && !line.natureCode)) {
      this.errorMessage = 'Completare le righe con importi e classificazione IVA dalla fattura originale.';
      return false;
    }
    return true;
  }

  private toRequest(): CreatePayableDocumentRequest {
    return {
      kind: this.kind,
      supplierId: this.kind === 'SupplierInvoice' ? this.supplierId || null : null,
      sourceDocumentNumber: this.sourceDocumentNumber.trim() || null,
      documentDate: this.documentDate,
      dueDate: this.dueDate,
      beneficiaryName: null,
      notes: this.notes.trim() || null,
      lines: this.draftLines.map(line => ({
        ...line,
        description: line.description.trim(),
        costCenter: line.costCenter?.trim() || null,
        project: line.project?.trim() || null,
        natureCode: line.natureCode?.trim() || null
      })),
      version: this.selected?.version,
      employeeId: this.kind === 'ExpenseReimbursement' ? this.employeeId : null
    };
  }

  private copyToDraft(document: PayableDocument): void {
    this.kind = document.kind;
    this.supplierId = document.supplierId ?? '';
    this.employeeId = document.employeeId ?? '';
    this.sourceDocumentNumber = document.sourceDocumentNumber ?? '';
    this.documentDate = document.documentDate;
    this.dueDate = document.dueDate;
    this.notes = document.notes ?? '';
    this.draftLines = document.lines.map(line => ({
      description: line.description,
      category: line.category,
      taxableAmount: line.taxableAmount,
      vatRate: line.vatRate,
      natureCode: line.natureCode,
      vatAmount: line.vatAmount,
      grossAmount: line.grossAmount,
      costCenter: line.costCenter,
      project: line.project
    }));
    if (this.draftLines.length === 0) {
      this.draftLines = [this.newLine()];
    }
  }

  private newLine(): PayableDocumentLineRequest {
    return {
      description: '',
      category: 'Other',
      taxableAmount: 0,
      vatRate: 22,
      natureCode: null,
      vatAmount: 0,
      grossAmount: 0,
      costCenter: null,
      project: null
    };
  }

  private restoreListState(): void {
    const params = this.route.snapshot.queryParamMap;
    this.search = params.get('search') ?? '';
    this.filterKind = params.get('kind') ?? '';
    this.filterStatus = params.get('status') ?? '';
    this.dateFrom = params.get('dateFrom') ?? '';
    this.dateTo = params.get('dateTo') ?? '';
    this.currentPage = Math.max(1, Number(params.get('page')) || 1);
    const requestedPageSize = Number(params.get('pageSize'));
    this.pageSize = [10, 20, 30, 50, 100].includes(requestedPageSize)
      ? requestedPageSize
      : 20;
  }

  private get listReturnUrl(): string {
    const path = this.reimbursementsOnly ? '/acquisti/rimborsi' : '/acquisti/passivi';
    const params = new URLSearchParams();
    if (!this.reimbursementsOnly) {
      if (this.search.trim()) params.set('search', this.search.trim());
      if (this.filterKind) params.set('kind', this.filterKind);
      if (this.filterStatus) params.set('status', this.filterStatus);
      if (this.dateFrom) params.set('dateFrom', this.dateFrom);
      if (this.dateTo) params.set('dateTo', this.dateTo);
    }
    params.set('page', String(this.currentPage));
    params.set('pageSize', String(this.pageSize));
    return `${path}?${params.toString()}`;
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
