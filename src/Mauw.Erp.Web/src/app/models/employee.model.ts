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
}

export interface EmployeePayrollCostRequest {
  period: string;
  monthlyCompanyCost: number;
  notes: string | null;
}

export type EmployeePage = PagedResult<Employee>;
