import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../services/auth.service';
import { MenuService } from '../../services/menu.service';
import { MenuItem } from '../../models/menu.model';

interface ShortcutSection {
  label: string;
  icon: string;
  links: MenuItem[];
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  sections: ShortcutSection[] = [];
  readonly today = new Intl.DateTimeFormat('it-IT', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric'
  }).format(new Date());

  constructor(
    private readonly menuService: MenuService,
    readonly auth: AuthService
  ) {}

  ngOnInit(): void {
    this.menuService.menu$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(items => {
        this.sections = items
          .filter(section => this.canSee(section))
          .map(section => ({
            label: section.label,
            icon: section.icon,
            links: (section.children ?? [])
              .filter(link => !link.isSeparator && !!link.route && this.canSee(link))
          }))
          .filter(section => section.links.length > 0);
      });

  }

  get firstName(): string {
    const name = this.auth.user().fullName ?? this.auth.user().email ?? 'utente';
    return name.split(/[ @]/)[0];
  }

  get roleLabel(): string {
    const labels: Record<string, string> = {
      Admin: 'Amministratore',
      Cashier: 'Cassa',
      Warehouse: 'Magazzino',
      Sales: 'Vendite',
      Accounting: 'Amministrazione'
    };

    return labels[this.auth.primaryRole] ?? 'Area personale';
  }

  canSee(item: MenuItem): boolean {
    return !item.roles?.length || item.roles.some(role => this.auth.hasRole(role));
  }

}
