import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { UserInfo } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly userSignal = signal<UserInfo>({ isAuthenticated: false });
  readonly user = this.userSignal.asReadonly();

  constructor(private http: HttpClient) {}

  async initialize(): Promise<void> {
    try {
      const info = await firstValueFrom(this.http.get<UserInfo>('/api/auth/user'));
      this.userSignal.set(info);
    } catch {
      this.userSignal.set({ isAuthenticated: false });
    }
  }

  login(): void {
    window.location.href = '/api/auth/login';
  }

  logout(): void {
    window.location.href = '/api/auth/logout';
  }

  get isAuthenticated(): boolean {
    return this.userSignal().isAuthenticated;
  }

  hasRole(role: string): boolean {
    return this.userSignal().roles?.includes(role) ?? false;
  }

  /** Costi e valorizzazioni: solo Admin e Accounting (il server li omette agli altri ruoli). */
  canSeeCosts(): boolean {
    return this.hasRole('Admin') || this.hasRole('Accounting');
  }


  get primaryRole(): string {
    return this.userSignal().roles?.[0] ?? '';
  }
}
