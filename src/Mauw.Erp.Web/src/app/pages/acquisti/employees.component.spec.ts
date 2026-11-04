import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import '../../locale';
import { Employee, EmployeeDocument, EmployeeTimeEntry } from '../../models/employee.model';
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
    streetAddress: null,
    postalCode: null,
    city: null,
    province: null,
    countryCode: null,
    email: null,
    phone: null,
    mobilePhone: null,
    isActive: true,
    notes: null,
    version: 'version-1'
  };

  beforeEach(async () => {
    employeesApi = jasmine.createSpyObj<EmployeeService>('EmployeeService', [
      'list', 'create', 'update', 'payrollCosts', 'createPayrollCost',
      'documents', 'createDocument', 'updateDocument', 'downloadDocument',
      'timeEntries', 'createTimeEntry', 'updateTimeEntry'
    ]);
    employeesApi.list.and.returnValue(of({
      items: [employee], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1
    }));
    employeesApi.payrollCosts.and.returnValue(of({
      items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0
    }));
    employeesApi.documents.and.returnValue(of({
      items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0
    }));
    employeesApi.timeEntries.and.returnValue(of({
      items: [], pageNumber: 1, pageSize: 31, totalCount: 0, totalPages: 0
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

  it('saves the employee address and contact information', () => {
    const component = fixture.componentInstance;
    component.createEmployee();
    component.employeeNumber = 'MAT-002';
    component.firstName = 'Luisa';
    component.lastName = 'Bianchi';
    component.annualCompanyCost = 48_000;
    component.streetAddress = 'Via Verdi 10';
    component.postalCode = '20121';
    component.city = 'Milano';
    component.province = 'MI';
    component.countryCode = 'IT';
    component.email = 'luisa.bianchi@example.it';
    component.phone = '+39 02 1234567';
    component.mobilePhone = '+39 333 1234567';
    employeesApi.create.and.returnValue(of(employee));

    component.saveEmployee();

    expect(employeesApi.create).toHaveBeenCalledWith(jasmine.objectContaining({
      streetAddress: 'Via Verdi 10',
      postalCode: '20121',
      city: 'Milano',
      province: 'MI',
      countryCode: 'IT',
      email: 'luisa.bianchi@example.it',
      phone: '+39 02 1234567',
      mobilePhone: '+39 333 1234567'
    }));
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

  it('archives a safety training certificate with its completion date', () => {
    const component = fixture.componentInstance;
    const file = new File(['%PDF-1.4'], 'sicurezza.pdf', { type: 'application/pdf' });
    const document: EmployeeDocument = {
      id: 'employee-document-1',
      employeeId: employee.id,
      kind: 'SafetyTraining',
      title: 'Formazione sicurezza',
      documentNumber: null,
      issuerOrProvider: 'Ente formatore',
      issuedOn: null,
      completedOn: '2026-10-15',
      expiresOn: '2028-10-15',
      notes: null,
      fileName: 'sicurezza.pdf',
      contentType: 'application/pdf',
      fileLength: 100,
      createdAtUtc: '2026-11-04T00:00:00Z',
      version: 'doc-version'
    };
    component.selectedEmployee = employee;
    component.documentKind = 'SafetyTraining';
    component.documentTitle = 'Formazione sicurezza';
    component.documentIssuerOrProvider = 'Ente formatore';
    component.documentCompletedOn = '2026-10-15';
    component.documentExpiresOn = '2028-10-15';
    component.employeeDocumentFile = file;
    employeesApi.createDocument.and.returnValue(of(document));

    component.saveEmployeeDocument();

    expect(employeesApi.createDocument).toHaveBeenCalledWith('employee-1', jasmine.objectContaining({
      kind: 'SafetyTraining',
      title: 'Formazione sicurezza',
      completedOn: '2026-10-15',
      expiresOn: '2028-10-15'
    }), file);
  });

  it('records a daily time card with start, end, and break', () => {
    const component = fixture.componentInstance;
    const entry: EmployeeTimeEntry = {
      id: 'entry-1',
      employeeId: employee.id,
      workDate: '2026-11-04',
      type: 'Work',
      startTime: '08:30:00',
      endTime: '17:00:00',
      breakMinutes: 30,
      workedMinutes: 480,
      notes: null,
      version: 'entry-version'
    };
    component.selectedEmployee = employee;
    component.timeWorkDate = '2026-11-04';
    component.timeType = 'Work';
    component.timeStart = '08:30';
    component.timeEnd = '17:00';
    component.timeBreakMinutes = 30;
    employeesApi.createTimeEntry.and.returnValue(of(entry));

    component.saveTimeEntry();

    expect(employeesApi.createTimeEntry).toHaveBeenCalledWith('employee-1', {
      workDate: '2026-11-04',
      type: 'Work',
      startTime: '08:30:00',
      endTime: '17:00:00',
      breakMinutes: 30,
      notes: null,
      version: undefined
    });
  });
});
