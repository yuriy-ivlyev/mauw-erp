import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmDeleteModalComponent } from '../../../shared/confirm-delete-modal/confirm-delete-modal.component';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SupportService, SizeDto, UpdateSizeRequest } from '../../../services/support.service';

@Component({
  selector: 'app-taglie',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ConfirmDeleteModalComponent],
  templateUrl: './taglie.component.html',
  styleUrl: './taglie.component.scss'
})
export class TaglieComponent implements OnInit {
  sizes: SizeDto[] = [];
  showForm = false;
  editingId: string | null = null;
  form!: FormGroup;
  errorMessage = '';
  successMessage = '';
  loading = false;
  deleteMessage = '';
  confirmDeleteId: string | null = null;

  constructor(
    private supportService: SupportService,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(1)]]
    });
  }

  ngOnInit() {
    this.loadSizes();
  }

  loadSizes() {
    this.loading = true;
    this.supportService.getSizes().subscribe({
      next: (data) => {
        this.sizes = data;
        this.loading = false;
      },
      error: (err) => {
        console.error('Errore caricamento taglie:', err);
        this.errorMessage = 'Errore nel caricamento delle taglie';
        this.loading = false;
      }
    });
  }

  openForm(size?: SizeDto) {
    if (size) {
      this.editingId = size.id;
      this.form.patchValue({
        name: size.name
      });
    } else {
      this.editingId = null;
      this.form.reset();
    }
    this.showForm = true;
    this.errorMessage = '';
    this.deleteMessage = '';
  }

  closeForm() {
    this.showForm = false;
    this.editingId = null;
    this.form.reset();
    this.errorMessage = '';
  }

  onSubmit() {
    if (!this.form.valid) return;

    this.loading = true;
    if (this.editingId) {
      const data: UpdateSizeRequest = {
        name: this.form.get('name')!.value,
        isVisible: this.sizes.find(s => s.id === this.editingId)?.isVisible || true
      };
      this.supportService.updateSize(this.editingId, data).subscribe({
        next: () => {
          this.successMessage = 'Taglia aggiornata con successo';
          this.loadSizes();
          this.closeForm();
          setTimeout(() => this.successMessage = '', 3000);
        },
        error: (err) => {
          this.errorMessage = err.error?.detail || 'Errore nell\'aggiornamento della taglia';
          this.loading = false;
        }
      });
    } else {
      const data = {
        name: this.form.get('name')!.value
      };
      this.supportService.createSize(data).subscribe({
        next: () => {
          this.successMessage = 'Taglia creata con successo';
          this.loadSizes();
          this.closeForm();
          setTimeout(() => this.successMessage = '', 3000);
        },
        error: (err) => {
          this.errorMessage = err.error?.detail || 'Errore nella creazione della taglia';
          this.loading = false;
        }
      });
    }
  }

  deleteSize(id: string) {
        this.loading = true;
    this.supportService.deleteSize(id).subscribe({
      next: () => {
        this.successMessage = 'Taglia eliminata con successo';
        this.loadSizes();
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: (err) => {
        if (err.status === 409) {
          this.deleteMessage = err.error?.detail || 'Questa taglia Ã¨ utilizzata in una o piÃ¹ varianti prodotto e non puÃ² essere eliminata. Puoi disattivarla dalla visibilitÃ .';
        } else {
          this.errorMessage = err.error?.detail || 'Errore nell\'eliminazione della taglia';
        }
        this.loading = false;
      }
    });
  }

  toggleVisibility(size: SizeDto) {
    const data: UpdateSizeRequest = {
      name: size.name,
      isVisible: !size.isVisible
    };
    this.supportService.updateSize(size.id, data).subscribe({
      next: () => {
        this.successMessage = size.isVisible ? 'Taglia nascosta' : 'Taglia visibile';
        this.loadSizes();
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: (err) => {
        this.errorMessage = 'Errore nell\'aggiornamento della visibilitÃ ';
      }
    });
  }


  askDelete(id: string): void { this.confirmDeleteId = id; }

  onDeleteConfirmed(): void {
    if (!this.confirmDeleteId) return;
    const id = this.confirmDeleteId;
    this.confirmDeleteId = null;
    this.deleteSize(id);
  }

  onDeleteCancelled(): void { this.confirmDeleteId = null; }
}


