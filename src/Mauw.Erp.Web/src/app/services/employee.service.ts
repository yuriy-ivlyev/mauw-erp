import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Employee,
  EmployeeDocument,
  EmployeeDocumentPage,
  EmployeeTimeEntry,
  EmployeeTimeEntryPage,
  EmployeePage,
  EmployeePayrollCostRequest,
  SaveEmployeeDocumentRequest,
  SaveEmployeeRequest,
  SaveEmployeeTimeEntryRequest
} from '../models/employee.model';
import { PayableDocumentPage } from '../models/payable-document.model';

@Injectable({ providedIn: 'root' })
export class EmployeeService {
  private readonly base = `${environment.apiUrl}/v1/employees`;

  constructor(private readonly http: HttpClient) {}

  list(
    page: number,
    pageSize: number,
    search = '',
    activeOnly = false
  ): Observable<EmployeePage> {
    let params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize))
      .set('activeOnly', String(activeOnly));
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<EmployeePage>(this.base, { params });
  }

  create(request: SaveEmployeeRequest): Observable<Employee> {
    return this.http.post<Employee>(this.base, request);
  }

  update(id: string, request: SaveEmployeeRequest): Observable<Employee> {
    return this.http.put<Employee>(`${this.base}/${id}`, request);
  }

  payrollCosts(employeeId: string, page: number, pageSize: number): Observable<PayableDocumentPage> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<PayableDocumentPage>(
      `${this.base}/${employeeId}/payroll-costs`, { params });
  }

  createPayrollCost(
    employeeId: string,
    request: EmployeePayrollCostRequest,
    file: File
  ): Observable<void> {
    const form = new FormData();
    form.append('period', request.period);
    form.append('monthlyCompanyCost', String(request.monthlyCompanyCost));
    if (request.notes) form.append('notes', request.notes);
    form.append('file', file, file.name);
    return this.http.post(`${this.base}/${employeeId}/payroll-costs`, form)
      .pipe(map(() => undefined));
  }

  documents(employeeId: string, page: number, pageSize: number): Observable<EmployeeDocumentPage> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<EmployeeDocumentPage>(
      `${this.base}/${employeeId}/documents`, { params });
  }

  createDocument(
    employeeId: string,
    request: SaveEmployeeDocumentRequest,
    file: File
  ): Observable<EmployeeDocument> {
    const form = new FormData();
    form.append('kind', request.kind);
    form.append('title', request.title);
    if (request.documentNumber) form.append('documentNumber', request.documentNumber);
    if (request.issuerOrProvider) form.append('issuerOrProvider', request.issuerOrProvider);
    if (request.issuedOn) form.append('issuedOn', request.issuedOn);
    if (request.completedOn) form.append('completedOn', request.completedOn);
    if (request.expiresOn) form.append('expiresOn', request.expiresOn);
    if (request.notes) form.append('notes', request.notes);
    form.append('file', file, file.name);
    return this.http.post<EmployeeDocument>(`${this.base}/${employeeId}/documents`, form);
  }

  updateDocument(
    employeeId: string,
    documentId: string,
    request: SaveEmployeeDocumentRequest
  ): Observable<EmployeeDocument> {
    return this.http.put<EmployeeDocument>(
      `${this.base}/${employeeId}/documents/${documentId}`, request);
  }

  downloadDocument(employeeId: string, documentId: string): Observable<Blob> {
    return this.http.get(`${this.base}/${employeeId}/documents/${documentId}/file`, {
      responseType: 'blob'
    });
  }

  timeEntries(
    employeeId: string,
    from: string,
    to: string,
    page: number,
    pageSize: number
  ): Observable<EmployeeTimeEntryPage> {
    const params = new HttpParams()
      .set('from', from)
      .set('to', to)
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<EmployeeTimeEntryPage>(
      `${this.base}/${employeeId}/time-entries`, { params });
  }

  createTimeEntry(
    employeeId: string,
    request: SaveEmployeeTimeEntryRequest
  ): Observable<EmployeeTimeEntry> {
    return this.http.post<EmployeeTimeEntry>(
      `${this.base}/${employeeId}/time-entries`, request);
  }

  updateTimeEntry(
    employeeId: string,
    entryId: string,
    request: SaveEmployeeTimeEntryRequest
  ): Observable<EmployeeTimeEntry> {
    return this.http.put<EmployeeTimeEntry>(
      `${this.base}/${employeeId}/time-entries/${entryId}`, request);
  }
}
