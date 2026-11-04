import { PagedResult } from './pagination.model';

export interface Employee {
  id: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  qualification: string | null;
  contractLevel: string | null;
  contractType: string | null;
  annualGrossSalary: number | null;
  annualCompanyCost: number | null;
  hireDate: string | null;
  endDate: string | null;
  streetAddress: string | null;
  postalCode: string | null;
  city: string | null;
  province: string | null;
  countryCode: string | null;
  email: string | null;
  phone: string | null;
  mobilePhone: string | null;
  isActive: boolean;
  notes: string | null;
  version: string;
}

export interface SaveEmployeeRequest {
  employeeNumber: string;
  firstName: string;
  lastName: string;
  qualification: string | null;
  contractLevel: string | null;
  contractType: string | null;
  annualGrossSalary: number | null;
  annualCompanyCost: number | null;
  hireDate: string | null;
  endDate: string | null;
  isActive: boolean;
  notes: string | null;
  version?: string;
  streetAddress: string | null;
  postalCode: string | null;
  city: string | null;
  province: string | null;
  countryCode: string | null;
  email: string | null;
  phone: string | null;
  mobilePhone: string | null;
}

export interface EmployeePayrollCostRequest {
  period: string;
  monthlyCompanyCost: number;
  notes: string | null;
}

export type EmployeeDocumentKind = 'IdentityDocument' | 'SafetyTraining' | 'Other';

export interface EmployeeDocument {
  id: string;
  employeeId: string;
  kind: EmployeeDocumentKind;
  title: string;
  documentNumber: string | null;
  issuerOrProvider: string | null;
  issuedOn: string | null;
  completedOn: string | null;
  expiresOn: string | null;
  notes: string | null;
  fileName: string;
  contentType: string;
  fileLength: number;
  createdAtUtc: string;
  version: string;
}

export interface SaveEmployeeDocumentRequest {
  kind: EmployeeDocumentKind;
  title: string;
  documentNumber: string | null;
  issuerOrProvider: string | null;
  issuedOn: string | null;
  completedOn: string | null;
  expiresOn: string | null;
  notes: string | null;
  version?: string;
}

export type EmployeeTimeEntryType =
  | 'Work'
  | 'Vacation'
  | 'SickLeave'
  | 'Permission'
  | 'OtherAbsence';

export interface EmployeeTimeEntry {
  id: string;
  employeeId: string;
  workDate: string;
  type: EmployeeTimeEntryType;
  startTime: string | null;
  endTime: string | null;
  breakMinutes: number;
  workedMinutes: number;
  notes: string | null;
  version: string;
}

export interface SaveEmployeeTimeEntryRequest {
  workDate: string;
  type: EmployeeTimeEntryType;
  startTime: string | null;
  endTime: string | null;
  breakMinutes: number;
  notes: string | null;
  version?: string;
}

export type EmployeePage = PagedResult<Employee>;
export type EmployeeDocumentPage = PagedResult<EmployeeDocument>;
export type EmployeeTimeEntryPage = PagedResult<EmployeeTimeEntry>;
