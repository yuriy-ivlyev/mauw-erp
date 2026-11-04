import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { Employee, SaveEmployeeRequest } from '../../models/employee.model';
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
    this.isActive = true;
    this.notes = '';
    this.editorOpen = true;
    this.successMessage = '';
    this.errorMessage = '';
    this.clearPayroll();
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
  }

  closeEditor(): void {
    this.editorOpen = false;
    this.selectedEmployee = null;
    this.clearPayroll();
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
      version: this.selectedEmployee?.version
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

  private clearPayroll(): void {
    this.payrollCosts = [];
    this.payrollTotalCount = 0;
    this.payrollPage = 1;
    this.payrollPeriod = '';
    this.monthlyCompanyCost = null;
    this.payrollNotes = '';
    this.payslip = null;
  }

  private describeError(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) return error.error.trim();
      if (typeof error.error?.detail === 'string') return error.error.detail;
    }
    return fallback;
  }
}
