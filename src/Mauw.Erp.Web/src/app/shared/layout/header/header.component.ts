import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../services/auth.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent {
  constructor(readonly auth: AuthService) {}

  get initials(): string {
    const name = this.auth.user().fullName ?? this.auth.user().email ?? 'U';
    return name.split(' ').map(p => p[0]).join('').slice(0, 2).toUpperCase();
  }

  logout(): void {
    this.auth.logout();
  }
}
