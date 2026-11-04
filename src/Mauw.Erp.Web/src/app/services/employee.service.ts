import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Employee,
  EmployeePage,
  EmployeePayrollCostRequest,
  SaveEmployeeRequest
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

  deactivate(id: string, version: string, endDate: string): Observable<Employee> {
    return this.http.post<Employee>(`${this.base}/${id}/deactivate`, { version, endDate });
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
}
