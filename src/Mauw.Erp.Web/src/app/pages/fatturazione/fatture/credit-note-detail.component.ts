import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject } from 'rxjs';
import { finalize, takeUntil } from 'rxjs/operators';
import { CreditNoteDetail } from '../../../models/credit-note.model';
import { CreditNoteService } from '../../../services/credit-note.service';
import { BackButtonComponent } from '../../../shared/back-button/back-button.component';
import { ConfirmDeleteModalComponent } from '../../../shared/confirm-delete-modal/confirm-delete-modal.component';
import { PageSlicePipe } from '../../../shared/pagination/page-slice.pipe';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';

@Component({
  selector: 'app-credit-note-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    BackButtonComponent,
    ConfirmDeleteModalComponent,
    PaginationComponent,
    PageSlicePipe
  ],
  templateUrl: './credit-note-detail.component.html'
})
export class CreditNoteDetailComponent implements OnInit, OnDestroy {
  creditNote: CreditNoteDetail | null = null;
  loading = true;
  transmitting = false;
  downloadingXml = false;
  confirmTransmitOpen = false;
  errorMessage = '';
  successMessage = '';
  returnUrl = '/fatturazione/fatture';
  itemsPage = 1;
  itemsPageSize = 20;

  private readonly destroy$ = new Subject<void>();

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly creditNotesApi: CreditNoteService
  ) {}

  ngOnInit(): void {
    this.route.queryParamMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const returnTo = params.get('returnTo');
      if (returnTo?.startsWith('/') && !returnTo.startsWith('//') && !returnTo.includes('\\')) {
        this.returnUrl = returnTo;
      }
    });

    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      const id = params.get('id');
      if (id) {
        this.loadCreditNote(id);
      } else {
        this.loading = false;
        this.errorMessage = 'Identificativo nota di credito non valido.';
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get currentUrl(): string {
    return this.router.url;
  }

  goBack(): void {
    void this.router.navigateByUrl(this.returnUrl);
  }

  onItemsPageSizeChange(pageSize: number): void {
    this.itemsPageSize = pageSize;
    this.itemsPage = 1;
  }

  loadCreditNote(id: string): void {
    this.loading = true;
    this.errorMessage = '';
    this.creditNotesApi.getById(id).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.loading = false; })
    ).subscribe({
      next: note => {
        this.creditNote = note;
        this.itemsPage = 1;
      },
      error: error => {
        this.errorMessage = this.describeError(error, 'Impossibile caricare la nota di credito.');
      }
    });
  }

  openTransmitConfirmation(): void {
    if (this.creditNote?.sdiStatus === 'NotTransmitted' && !this.transmitting) {
      this.confirmTransmitOpen = true;
    }
  }

  confirmTransmission(): void {
    const note = this.creditNote;
    this.confirmTransmitOpen = false;
    if (!note || note.sdiStatus !== 'NotTransmitted' || this.transmitting) {
      return;
    }

    this.transmitting = true;
    this.errorMessage = '';
    this.creditNotesApi.transmitToSdi(note.id).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.transmitting = false; })
    ).subscribe({
      next: transmitted => {
        this.successMessage = `Nota ${transmitted.creditNoteNumber} trasmessa allo SDI.`;
        this.loadCreditNote(note.id);
      },
      error: error => {
        this.errorMessage = this.describeError(error, 'Impossibile trasmettere la nota di credito.');
      }
    });
  }

  downloadXml(): void {
    const note = this.creditNote;
    if (!note || this.downloadingXml) {
      return;
    }

    this.downloadingXml = true;
    this.errorMessage = '';
    this.creditNotesApi.downloadXml(note.id).pipe(
      takeUntil(this.destroy$),
      finalize(() => { this.downloadingXml = false; })
    ).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = note.sdiFileName || `nota-di-credito-${note.creditNoteNumber}.xml`;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: error => {
        this.errorMessage = this.describeError(error, 'Impossibile scaricare l’XML TD04.');
      }
    });
  }

  sdiStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      NotTransmitted: 'Non trasmessa',
      Pending: 'In coda',
      Transmitted: 'Trasmessa',
      Accepted: 'Accettata'
    };
    return labels[status] ?? status;
  }

  sdiStatusClass(status: string): string {
    const classes: Record<string, string> = {
      NotTransmitted: 'bg-slate-100 text-slate-700',
      Pending: 'bg-amber-100 text-amber-800',
      Transmitted: 'bg-blue-100 text-blue-800',
      Accepted: 'bg-green-100 text-green-800'
    };
    return classes[status] ?? 'bg-slate-100 text-slate-700';
  }

  private describeError(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) {
        return error.error.trim();
      }
      if (error.error instanceof Blob) {
        void error.error.text().then(text => {
          this.errorMessage = text.trim() || fallback;
        });
      } else if (typeof error.error?.message === 'string' && error.error.message.trim()) {
        return error.error.message.trim();
      }
    }
    return fallback;
  }
}
