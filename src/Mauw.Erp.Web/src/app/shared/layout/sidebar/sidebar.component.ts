import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MenuService } from '../../../services/menu.service';
import { AuthService } from '../../../services/auth.service';
import { MenuItem } from '../../../models/menu.model';
import { CompanyBrandingService } from '../../../services/company-branding.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  sidebarOpen = true;
  menuItems: MenuItem[] = [];

  constructor(
    private menuService: MenuService,
    private auth: AuthService,
    readonly branding: CompanyBrandingService
  ) {}

  ngOnInit(): void {
    this.menuService.menu$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(items => {
      this.menuItems = items
        .filter(item => this.canSee(item))
        .filter(item => !item.children?.length ||
          item.children.some(child => !child.isSeparator && this.canSee(child)));
    });
  }

  /** True se l'utente ha almeno uno dei ruoli richiesti (o nessun ruolo specificato). */
  canSee(item: MenuItem): boolean {
    if (!item.roles || item.roles.length === 0) return true;
    return item.roles.some(r => this.auth.hasRole(r));
  }

  toggleSidebar(): void {
    this.sidebarOpen = !this.sidebarOpen;
  }

  toggleSubmenu(item: MenuItem): void {
    this.menuService.toggleCollapse(item);
  }

  getSvgPath(icon: string): string {
    return icon;
  }
}
