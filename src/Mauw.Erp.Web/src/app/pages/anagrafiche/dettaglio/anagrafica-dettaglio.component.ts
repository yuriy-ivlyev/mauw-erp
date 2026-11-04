import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { ContactDetail } from '../../../models/contact.model';
import { ContactService } from '../../../services/contact.service';
import { BackButtonComponent } from '../../../shared/back-button/back-button.component';
import { CustomerStatementComponent } from '../../../components/customer-statement/customer-statement.component';

type ContactDetailTab = 'details' | 'statement';

@Component({
  selector: 'app-anagrafica-dettaglio',
  standalone: true,
  imports: [CommonModule, RouterLink, BackButtonComponent, CustomerStatementComponent],
  templateUrl: './anagrafica-dettaglio.component.html'
})
export class AnagraficaDettaglioComponent implements OnInit, OnDestroy {
  contact: ContactDetail | null = null;
  activeTab: ContactDetailTab = 'statement';
  loadingContact = true;
  errorMessage = '';

  private readonly destroy$ = new Subject<void>();
  private contactRequestVersion = 0;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly contactsApi: ContactService
  ) {}

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const id = params.get('id');
      if (!id) {
        this.loadingContact = false;
        this.errorMessage = 'Identificativo anagrafica non valido.';
        return;
      }

      this.loadContact(id);
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  goBack(): void {
    void this.router.navigate(['/anagrafiche/elenco']);
  }

  editContact(): void {
    if (this.contact) {
      void this.router.navigate(['/anagrafiche', this.contact.id, 'modifica']);
    }
  }

  selectTab(tab: ContactDetailTab): void {
    this.activeTab = tab;
  }

  private loadContact(id: string): void {
    const requestVersion = ++this.contactRequestVersion;
    this.contact = null;
    this.loadingContact = true;
    this.errorMessage = '';

    this.contactsApi.getById(id).pipe(
      takeUntil(this.destroy$),
      finalize(() => {
        if (requestVersion === this.contactRequestVersion) {
          this.loadingContact = false;
        }
      })
    ).subscribe({
      next: contact => {
        if (requestVersion !== this.contactRequestVersion) {
          return;
        }
        this.contact = contact;
        if (contact.customerProfile) {
          this.activeTab = this.route.snapshot.queryParamMap.get('tab') === 'details'
            ? 'details'
            : 'statement';
        } else {
          this.activeTab = 'details';
        }
      },
      error: error => {
        if (requestVersion === this.contactRequestVersion) {
          this.errorMessage = this.describeError(error, 'Impossibile caricare l’anagrafica.');
        }
      }
    });
  }

  private describeError(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        return error.error.trim();
      }
      if (typeof error.error?.detail === 'string') {
        return error.error.detail;
      }
    }
    return fallback;
  }
}
