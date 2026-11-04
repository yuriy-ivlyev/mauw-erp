import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import '../../locale';
import { Employee } from '../../models/employee.model';
import { EmployeeService } from '../../services/employee.service';
import { PayableDocumentService } from '../../services/payable-document.service';
import { EmployeesComponent } from './employees.component';

describe('EmployeesComponent', () => {
  let fixture: ComponentFixture<EmployeesComponent>;
  let employeesApi: jasmine.SpyObj<EmployeeService>;

  const employee: Employee = {
    id: 'employee-1',
    employeeNumber: 'MAT-001',
    firstName: 'Mario',
    lastName: 'Rossi',
    qualification: 'Impiegato',
    contractLevel: '4',
    contractType: 'Commercio',
    annualGrossSalary: 32_000,
    annualCompanyCost: 45_000,
    hireDate: '2024-01-01',
    endDate: null,
    isActive: true,
    notes: null,
    version: 'version-1'
  };

  beforeEach(async () => {
    employeesApi = jasmine.createSpyObj<EmployeeService>('EmployeeService', [
      'list', 'create', 'update', 'deactivate', 'payrollCosts', 'createPayrollCost'
    ]);
    employeesApi.list.and.returnValue(of({
      items: [employee], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1
    }));
    employeesApi.payrollCosts.and.returnValue(of({
      items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0
    }));

    await TestBed.configureTestingModule({
      imports: [EmployeesComponent],
      providers: [
        { provide: EmployeeService, useValue: employeesApi },
        { provide: PayableDocumentService, useValue: jasmine.createSpyObj('PayableDocumentService', ['downloadAttachment']) }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(EmployeesComponent);
    fixture.detectChanges();
  });

  it('loads employee records and provides the paginated admin list', () => {
    expect(employeesApi.list).toHaveBeenCalledWith(1, 20, '');
    expect(fixture.nativeElement.textContent).toContain('MAT-001');
    expect(fixture.nativeElement.querySelector('app-pagination')).not.toBeNull();
  });

  it('registers a monthly company cost together with the payslip', () => {
    const component = fixture.componentInstance;
    const file = new File(['%PDF-1.4'], 'cedolino.pdf', { type: 'application/pdf' });
    component.selectedEmployee = employee;
    component.payrollPeriod = '2026-11';
    component.monthlyCompanyCost = 3_250.75;
    component.payrollNotes = 'Costo complessivo';
    component.payslip = file;
    employeesApi.createPayrollCost.and.returnValue(of(undefined));

    component.savePayrollCost();

    expect(employeesApi.createPayrollCost).toHaveBeenCalledWith('employee-1', {
      period: '2026-11-01',
      monthlyCompanyCost: 3_250.75,
      notes: 'Costo complessivo'
    }, file);
    expect(component.payrollPeriod).toBe('');
    expect(component.monthlyCompanyCost).toBeNull();
  });
});
