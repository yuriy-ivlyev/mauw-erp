export interface UserInfo {
  isAuthenticated: boolean;
  userId?:   string;
  email?:    string;
  fullName?: string;
  roles?:    string[];
}

export type AppRole = 'Admin' | 'Cashier' | 'Warehouse' | 'Sales' | 'Accounting';
