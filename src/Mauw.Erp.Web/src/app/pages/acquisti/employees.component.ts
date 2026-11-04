import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import {
  Employee,
  EmployeeDocument,
  EmployeeDocumentKind,
  EmployeeTimeEntry,
  EmployeeTimeEntryType,
  SaveEmployeeDocumentRequest,
  SaveEmployeeRequest,
  SaveEmployeeTimeEntryRequest
} from '../../models/employee.model';
import { PayableDocumentListItem } from '../../models/payable-document.model';
import { EmployeeService } from '../../services/employee.service';
import { PayableDocumentService } from '../../services/payable-document.service';
import { AddActionButtonComponent } from '../../shared/add-action-button/add-action-button.component';
import { BackButtonComponent } from '../../shared/back-button/back-button.component';
import { PaginationComponent } from '../../shared/pagination/pagination.component';
import { TableActionButtonComponent } from '../../shared/table-action-button/table-action-button.component';

function localDate(value: Date): string {
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${value.getFullYear()}-${month}-${day}`;
}

function currentMonth(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
}

@Component({
  selector: 'app-employees',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    AddActionButtonComponent,
    BackButtonComponent,
    PaginationComponent,
    TableActionButtonComponent
  ],
  templateUrl: './employees.component.html'
})
export class EmployeesComponent implements OnInit {
  @ViewChild('payslipInput') private payslipInput?: ElementRef<HTMLInputElement>;
  @ViewChild('employeeDocumentFileInput') private employeeDocumentFileInput?: ElementRef<HTMLInputElement>;

  employees: Employee[] = [];
  currentPage = 1;
  pageSize = 20;
  totalCount = 0;
  search = '';
  loading = false;
  saving = false;
  errorMessage = '';
  successMessage = '';

  editorOpen = false;
  selectedEmployee: Employee | null = null;
  employeeNumber = '';
  firstName = '';
  lastName = '';
  qualification = '';
  contractLevel = '';
  contractType = '';
  annualGrossSalary: number | null = null;
  annualCompanyCost: number | null = null;
  hireDate = '';
  endDate = '';
  streetAddress = '';
  postalCode = '';
  city = '';
  province = '';
  countryCode = 'IT';
  email = '';
  phone = '';
  mobilePhone = '';
  isActive = true;
  notes = '';

  payrollCosts: PayableDocumentListItem[] = [];
  payrollPage = 1;
  payrollPageSize = 10;
  payrollTotalCount = 0;
  payrollLoading = false;
  payrollSaving = false;
  payrollPeriod = '';
  monthlyCompanyCost: number | null = null;
  payrollNotes = '';
  payslip: File | null = null;

  employeeDocuments: EmployeeDocument[] = [];
  documentsPage = 1;
  documentsPageSize = 10;
  documentsTotalCount = 0;
  documentsLoading = false;
  documentSaving = false;
  documentKind: EmployeeDocumentKind = 'IdentityDocument';
  documentTitle = '';
  documentNumber = '';
  documentIssuerOrProvider = '';
  documentIssuedOn = '';
  documentCompletedOn = '';
  documentExpiresOn = '';
  documentNotes = '';
  employeeDocumentFile: File | null = null;

  timeEntries: EmployeeTimeEntry[] = [];
  timePage = 1;
  timePageSize = 31;
  timeTotalCount = 0;
  timeEntriesLoading = false;
  timeEntrySaving = false;
  timeMonth = currentMonth();
  editingTimeEntry: EmployeeTimeEntry | null = null;
  timeWorkDate = localDate(new Date());
  timeType: EmployeeTimeEntryType = 'Work';
  timeStart = '08:30';
  timeEnd = '17:30';
  timeBreakMinutes = 30;
  timeNotes = '';

  constructor(
    private readonly employeesApi: EmployeeService,
    private readonly payablesApi: PayableDocumentService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.errorMessage = '';
    this.employeesApi.list(this.currentPage, this.pageSize, this.search)
      .pipe(finalize(() => { this.loading = false; }))
      .subscribe({
        next: result => {
          this.employees = result.items;
          this.totalCount = result.totalCount;
        },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile caricare i dipendenti.'); }
      });
  }

  applySearch(): void {
    this.currentPage = 1;
    this.load();
  }

  changePage(page: number): void {
    this.currentPage = page;
    this.load();
  }

  changePageSize(pageSize: number): void {
    this.pageSize = pageSize;
    this.currentPage = 1;
    this.load();
  }

  createEmployee(): void {
    this.selectedEmployee = null;
    this.employeeNumber = '';
    this.firstName = '';
    this.lastName = '';
    this.qualification = '';
    this.contractLevel = '';
    this.contractType = '';
    this.annualGrossSalary = null;
    this.annualCompanyCost = null;
    this.hireDate = '';
    this.endDate = '';
    this.streetAddress = '';
    this.postalCode = '';
    this.city = '';
    this.province = '';
    this.countryCode = 'IT';
    this.email = '';
    this.phone = '';
    this.mobilePhone = '';
    this.isActive = true;
    this.notes = '';
    this.editorOpen = true;
    this.successMessage = '';
    this.errorMessage = '';
    this.clearPayroll();
    this.clearEmployeeRecords();
  }

  editEmployee(employee: Employee): void {
    this.selectedEmployee = employee;
    this.employeeNumber = employee.employeeNumber;
    this.firstName = employee.firstName;
    this.lastName = employee.lastName;
    this.qualification = employee.qualification ?? '';
    this.contractLevel = employee.contractLevel ?? '';
    this.contractType = employee.contractType ?? '';
    this.annualGrossSalary = employee.annualGrossSalary;
    this.annualCompanyCost = employee.annualCompanyCost;
    this.hireDate = employee.hireDate ?? '';
    this.endDate = employee.endDate ?? '';
    this.streetAddress = employee.streetAddress ?? '';
    this.postalCode = employee.postalCode ?? '';
    this.city = employee.city ?? '';
    this.province = employee.province ?? '';
    this.countryCode = employee.countryCode ?? 'IT';
    this.email = employee.email ?? '';
    this.phone = employee.phone ?? '';
    this.mobilePhone = employee.mobilePhone ?? '';
    this.isActive = employee.isActive;
    this.notes = employee.notes ?? '';
    this.editorOpen = true;
    this.successMessage = '';
    this.errorMessage = '';
    this.payrollPage = 1;
    this.loadPayrollCosts(employee);
  }

  openPayroll(employee: Employee): void {
    this.selectedEmployee = employee;
    this.editorOpen = false;
    this.successMessage = '';
    this.errorMessage = '';
    this.payrollPage = 1;
    this.loadPayrollCosts(employee);
    this.documentsPage = 1;
    this.loadEmployeeDocuments(employee);
    this.timePage = 1;
    this.loadTimeEntries(employee);
  }

  closeEditor(): void {
    this.editorOpen = false;
    this.selectedEmployee = null;
    this.clearPayroll();
    this.clearEmployeeRecords();
  }

  saveEmployee(): void {
    if (this.saving) return;
    if (!this.employeeNumber.trim() || !this.firstName.trim() || !this.lastName.trim()) {
      this.errorMessage = 'Matricola, nome e cognome sono obbligatori.';
      return;
    }
    if (this.annualGrossSalary === null && this.annualCompanyCost === null) {
      this.errorMessage = 'Inserire almeno la RAL o il costo annuo aziendale.';
      return;
    }
    if (this.isActive && this.endDate) {
      this.errorMessage = 'Un dipendente cessato non può risultare attivo.';
      return;
    }
    const hasAddress = this.streetAddress.trim() || this.postalCode.trim() || this.city.trim();
    if (hasAddress && (!this.streetAddress.trim() || !this.postalCode.trim() || !this.city.trim())) {
      this.errorMessage = 'Per salvare l’indirizzo indicare via, CAP e città.';
      return;
    }

    const request: SaveEmployeeRequest = {
      employeeNumber: this.employeeNumber.trim(),
      firstName: this.firstName.trim(),
      lastName: this.lastName.trim(),
      qualification: this.qualification.trim() || null,
      contractLevel: this.contractLevel.trim() || null,
      contractType: this.contractType.trim() || null,
      annualGrossSalary: this.annualGrossSalary,
      annualCompanyCost: this.annualCompanyCost,
      hireDate: this.hireDate || null,
      endDate: this.endDate || null,
      isActive: this.isActive,
      notes: this.notes.trim() || null,
      version: this.selectedEmployee?.version,
      streetAddress: this.streetAddress.trim() || null,
      postalCode: this.postalCode.trim() || null,
      city: this.city.trim() || null,
      province: this.province.trim() || null,
      countryCode: this.countryCode.trim() || null,
      email: this.email.trim() || null,
      phone: this.phone.trim() || null,
      mobilePhone: this.mobilePhone.trim() || null
    };
    this.saving = true;
    this.errorMessage = '';
    const save = this.selectedEmployee
      ? this.employeesApi.update(this.selectedEmployee.id, request)
      : this.employeesApi.create(request);
    save.pipe(finalize(() => { this.saving = false; }))
      .subscribe({
        next: employee => {
          this.selectedEmployee = employee;
          this.successMessage = 'Dati del dipendente salvati.';
          this.load();
          this.loadPayrollCosts(employee);
        },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile salvare il dipendente.'); }
      });
  }

  onPayslipSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.payslip = input.files?.[0] ?? null;
  }

  savePayrollCost(): void {
    const employee = this.selectedEmployee;
    if (!employee || !this.payrollPeriod || !this.payslip || this.payrollSaving) {
      this.errorMessage = 'Selezionare mese, cedolino e dipendente.';
      return;
    }
    if (this.monthlyCompanyCost === null || this.monthlyCompanyCost <= 0) {
      this.errorMessage = 'Il costo aziendale mensile deve essere positivo.';
      return;
    }

    this.payrollSaving = true;
    this.errorMessage = '';
    this.employeesApi.createPayrollCost(employee.id, {
      period: `${this.payrollPeriod}-01`,
      monthlyCompanyCost: this.monthlyCompanyCost,
      notes: this.payrollNotes.trim() || null
    }, this.payslip).pipe(finalize(() => { this.payrollSaving = false; }))
      .subscribe({
        next: () => {
          this.successMessage = 'Costo mensile e cedolino registrati.';
          this.payrollPeriod = '';
          this.monthlyCompanyCost = null;
          this.payrollNotes = '';
          this.payslip = null;
          if (this.payslipInput) this.payslipInput.nativeElement.value = '';
          this.loadPayrollCosts(employee);
        },
        error: error => {
          this.errorMessage = this.describeError(error, 'Impossibile registrare il costo mensile e il cedolino.');
        }
      });
  }

  changePayrollPage(page: number): void {
    this.payrollPage = page;
    if (this.selectedEmployee) this.loadPayrollCosts(this.selectedEmployee);
  }

  changePayrollPageSize(pageSize: number): void {
    this.payrollPageSize = pageSize;
    this.payrollPage = 1;
    if (this.selectedEmployee) this.loadPayrollCosts(this.selectedEmployee);
  }

  downloadPayslip(document: PayableDocumentListItem): void {
    this.payablesApi.downloadAttachment(document.id)
      .subscribe({
        next: blob => {
          const url = URL.createObjectURL(blob);
          const link = window.document.createElement('a');
          link.href = url;
          link.download = document.attachmentFileName ?? `${document.documentCode}.pdf`;
          link.click();
          window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
        },
        error: error => { this.errorMessage = this.describeError(error, 'Impossibile scaricare il cedolino.'); }
      });
  }

  onEmployeeDocumentSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.employeeDocumentFile = input.files?.[0] ?? null;
  }

  saveEmployeeDocument(): void {
    const employee = this.selectedEmployee;
    if (!employee || this.documentSaving || !this.documentTitle.trim()) {
      this.errorMessage = 'Selezionare un dipendente e indicare il titolo del documento.';
      return;
    }
    if (!this.employeeDocumentFile) {
      this.errorMessage = 'Selezionare il file da archiviare.';
      return;
    }
    if (this.documentKind === 'IdentityDocument' && !this.documentNumber.trim()) {
      this.errorMessage = 'Il numero del documento di identità è obbligatorio.';
      return;
    }
    if (this.documentKind === 'SafetyTraining' && !this.documentCompletedOn) {
      this.errorMessage = 'Indicare la data di completamento del corso.';
      return;
    }

    const request: SaveEmployeeDocumentRequest = {
      kind: this.documentKind,
      title: this.documentTitle.trim(),
      documentNumber: this.documentNumber.trim() || null,
      issuerOrProvider: this.documentIssuerOrProvider.trim() || null,
      issuedOn: this.documentIssuedOn || null,
      completedOn: this.documentCompletedOn || null,
      expiresOn: this.documentExpiresOn || null,
      notes: this.documentNotes.trim() || null
    };
    this.documentSaving = true;
    this.errorMessage = '';
    this.employeesApi.createDocument(employee.id, request, this.employeeDocumentFile)
      .pipe(finalize(() => { this.documentSaving = false; }))
      .subscribe({
        next: () => {
          this.successMessage = 'Documento dipendente archiviato.';
          this.clearDocumentForm();
          this.loadEmployeeDocuments(employee);
        },
        error: error => {
          this.errorMessage = this.describeError(error, 'Impossibile archiviare il documento dipendente.');
        }
      });
  }

  changeDocumentsPage(page: number): void {
    this.documentsPage = page;
    if (this.selectedEmployee) this.loadEmployeeDocuments(this.selectedEmployee);
  }

  changeDocumentsPageSize(pageSize: number): void {
    this.documentsPageSize = pageSize;
    this.documentsPage = 1;
    if (this.selectedEmployee) this.loadEmployeeDocuments(this.selectedEmployee);
  }

  downloadEmployeeDocument(document: EmployeeDocument): void {
    const employee = this.selectedEmployee;
    if (!employee) return;
    this.employeesApi.downloadDocument(employee.id, document.id)
      .subscribe({
        next: blob => {
          const url = URL.createObjectURL(blob);
          const link = window.document.createElement('a');
          link.href = url;
          link.download = document.fileName;
          link.click();
          window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
        },
        error: error => {
          this.errorMessage = this.describeError(error, 'Impossibile scaricare il documento dipendente.');
        }
      });
  }

  documentKindLabel(kind: string): string {
    const labels: Record<string, string> = {
      IdentityDocument: 'Documento identità',
      SafetyTraining: 'Corso sicurezza',
      Other: 'Altro'
    };
    return labels[kind] ?? kind;
  }

  saveTimeEntry(): void {
    const employee = this.selectedEmployee;
    if (!employee || this.timeEntrySaving || !this.timeWorkDate) {
      this.errorMessage = 'Selezionare il dipendente e la data del cartellino.';
      return;
    }
    if (this.timeType === 'Work'
      && (!this.timeStart || !this.timeEnd || this.timeEnd <= this.timeStart)) {
      this.errorMessage = 'Per una giornata lavorata l’uscita deve essere successiva all’entrata.';
      return;
    }

    const request: SaveEmployeeTimeEntryRequest = {
      workDate: this.timeWorkDate,
      type: this.timeType,
      startTime: this.timeType === 'Work' ? `${this.timeStart}:00` : null,
      endTime: this.timeType === 'Work' ? `${this.timeEnd}:00` : null,
      breakMinutes: this.timeType === 'Work' ? this.timeBreakMinutes : 0,
      notes: this.timeNotes.trim() || null,
      version: this.editingTimeEntry?.version
    };
    this.timeEntrySaving = true;
    this.errorMessage = '';
    const save = this.editingTimeEntry
      ? this.employeesApi.updateTimeEntry(employee.id, this.editingTimeEntry.id, request)
      : this.employeesApi.createTimeEntry(employee.id, request);
    save.pipe(finalize(() => { this.timeEntrySaving = false; }))
      .subscribe({
        next: () => {
          this.successMessage = this.editingTimeEntry
            ? 'Cartellino aggiornato.'
            : 'Cartellino registrato.';
          this.clearTimeEntryForm();
          this.loadTimeEntries(employee);
        },
        error: error => {
          this.errorMessage = this.describeError(error, 'Impossibile salvare il cartellino.');
        }
      });
  }

  editTimeEntry(entry: EmployeeTimeEntry): void {
    this.editingTimeEntry = entry;
    this.timeWorkDate = entry.workDate;
    this.timeType = entry.type;
    this.timeStart = entry.startTime?.slice(0, 5) ?? '';
    this.timeEnd = entry.endTime?.slice(0, 5) ?? '';
    this.timeBreakMinutes = entry.breakMinutes;
    this.timeNotes = entry.notes ?? '';
  }

  cancelTimeEntryEdit(): void {
    this.clearTimeEntryForm();
  }

  changeTimeMonth(): void {
    if (!this.timeMonth) return;
    this.timePage = 1;
    this.timeWorkDate = `${this.timeMonth}-01`;
    if (this.selectedEmployee) this.loadTimeEntries(this.selectedEmployee);
  }

  changeTimePage(page: number): void {
    this.timePage = page;
    if (this.selectedEmployee) this.loadTimeEntries(this.selectedEmployee);
  }

  changeTimePageSize(pageSize: number): void {
    this.timePageSize = pageSize;
    this.timePage = 1;
    if (this.selectedEmployee) this.loadTimeEntries(this.selectedEmployee);
  }

  formatWorkedMinutes(minutes: number): string {
    return `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
  }

  timeTypeLabel(type: EmployeeTimeEntryType): string {
    const labels: Record<EmployeeTimeEntryType, string> = {
      Work: 'Lavoro',
      Vacation: 'Ferie',
      SickLeave: 'Malattia',
      Permission: 'Permesso',
      OtherAbsence: 'Altra assenza'
    };
    return labels[type];
  }

  private loadPayrollCosts(employee: Employee): void {
    this.payrollLoading = true;
    this.payrollCosts = [];
    this.employeesApi.payrollCosts(
      employee.id, this.payrollPage, this.payrollPageSize)
      .pipe(finalize(() => { this.payrollLoading = false; }))
      .subscribe({
        next: result => {
          this.payrollCosts = result.items;
          this.payrollTotalCount = result.totalCount;
        },
        error: error => {
          this.errorMessage = this.describeError(error, 'Impossibile caricare i costi mensili.');
        }
      });
  }

  private loadEmployeeDocuments(employee: Employee): void {
    this.documentsLoading = true;
    this.employeesApi.documents(employee.id, this.documentsPage, this.documentsPageSize)
      .pipe(finalize(() => { this.documentsLoading = false; }))
      .subscribe({
        next: result => {
          this.employeeDocuments = result.items;
          this.documentsTotalCount = result.totalCount;
        },
        error: error => {
          this.errorMessage = this.describeError(error, 'Impossibile caricare i documenti dipendente.');
        }
      });
  }

  private loadTimeEntries(employee: Employee): void {
    const range = this.selectedMonthRange();
    this.timeEntriesLoading = true;
    this.employeesApi.timeEntries(
      employee.id, range.from, range.to, this.timePage, this.timePageSize)
      .pipe(finalize(() => { this.timeEntriesLoading = false; }))
      .subscribe({
        next: result => {
          this.timeEntries = result.items;
          this.timeTotalCount = result.totalCount;
        },
        error: error => {
          this.errorMessage = this.describeError(error, 'Impossibile caricare i cartellini.');
        }
      });
  }

  private selectedMonthRange(): { from: string; to: string } {
    const [year, month] = this.timeMonth.split('-').map(Number);
    const monthValue = String(month).padStart(2, '0');
    const lastDay = new Date(year, month, 0).getDate();
    return {
      from: `${year}-${monthValue}-01`,
      to: `${year}-${monthValue}-${String(lastDay).padStart(2, '0')}`
    };
  }

  private clearDocumentForm(): void {
    this.documentKind = 'IdentityDocument';
    this.documentTitle = '';
    this.documentNumber = '';
    this.documentIssuerOrProvider = '';
    this.documentIssuedOn = '';
    this.documentCompletedOn = '';
    this.documentExpiresOn = '';
    this.documentNotes = '';
    this.employeeDocumentFile = null;
    if (this.employeeDocumentFileInput)
      this.employeeDocumentFileInput.nativeElement.value = '';
  }

  private clearTimeEntryForm(): void {
    this.editingTimeEntry = null;
    this.timeWorkDate = this.timeMonth ? `${this.timeMonth}-01` : localDate(new Date());
    this.timeType = 'Work';
    this.timeStart = '08:30';
    this.timeEnd = '17:30';
    this.timeBreakMinutes = 30;
    this.timeNotes = '';
  }

  private clearEmployeeRecords(): void {
    this.employeeDocuments = [];
    this.documentsPage = 1;
    this.documentsTotalCount = 0;
    this.timeEntries = [];
    this.timePage = 1;
    this.timeTotalCount = 0;
    this.clearDocumentForm();
    this.clearTimeEntryForm();
  }

  private clearPayroll(): void {
    this.payrollCosts = [];
    this.payrollTotalCount = 0;
    this.payrollPage = 1;
    this.payrollPeriod = '';
    this.monthlyCompanyCost = null;
    this.payrollNotes = '';
    this.payslip = null;
    if (this.payslipInput) this.payslipInput.nativeElement.value = '';
  }

  private describeError(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) return error.error.trim();
      if (typeof error.error?.detail === 'string') return error.error.detail;
    }
    return fallback;
  }
}
