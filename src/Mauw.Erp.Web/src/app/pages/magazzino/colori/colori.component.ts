import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmDeleteModalComponent } from '../../../shared/confirm-delete-modal/confirm-delete-modal.component';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SupportService, ColorDto, CreateColorRequest, UpdateColorRequest } from '../../../services/support.service';

@Component({
  selector: 'app-colori',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ConfirmDeleteModalComponent],
  templateUrl: './colori.component.html',
  styleUrl: './colori.component.scss'
})
export class ColoriComponent implements OnInit {
  colors: ColorDto[] = [];
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
      name: ['', [Validators.required, Validators.minLength(1)]],
      htmlColor: ['#000000', Validators.required]
    });
  }

  ngOnInit() {
    this.loadColors();
  }

  loadColors() {
    this.loading = true;
    this.supportService.getColors().subscribe({
      next: (data) => {
        this.colors = data;
        this.loading = false;
      },
      error: (err) => {
        console.error('Errore caricamento colori:', err);
        this.errorMessage = 'Errore nel caricamento dei colori';
        this.loading = false;
      }
    });
  }

  openForm(color?: ColorDto) {
    if (color) {
      this.editingId = color.id;
      this.form.patchValue({
        name: color.name,
        htmlColor: color.htmlColor
      });
    } else {
      this.editingId = null;
      this.form.reset({ htmlColor: '#000000' });
    }
    this.showForm = true;
    this.errorMessage = '';
    this.deleteMessage = '';
  }

  closeForm() {
    this.showForm = false;
    this.editingId = null;
    this.form.reset({ htmlColor: '#000000' });
    this.errorMessage = '';
  }

  onSubmit() {
    if (!this.form.valid) return;

    this.loading = true;
    if (this.editingId) {
      const data: UpdateColorRequest = {
        name: this.form.get('name')!.value,
        htmlColor: this.form.get('htmlColor')!.value,
        isVisible: this.colors.find(c => c.id === this.editingId)?.isVisible || true
      };
      this.supportService.updateColor(this.editingId, data).subscribe({
        next: () => {
          this.successMessage = 'Colore aggiornato con successo';
          this.loadColors();
          this.closeForm();
          setTimeout(() => this.successMessage = '', 3000);
        },
        error: (err) => {
          this.errorMessage = err.error?.detail || 'Errore nell\'aggiornamento del colore';
          this.loading = false;
        }
      });
    } else {
      const data: CreateColorRequest = {
        name: this.form.get('name')!.value,
        htmlColor: this.form.get('htmlColor')!.value
      };
      this.supportService.createColor(data).subscribe({
        next: () => {
          this.successMessage = 'Colore creato con successo';
          this.loadColors();
          this.closeForm();
          setTimeout(() => this.successMessage = '', 3000);
        },
        error: (err) => {
          this.errorMessage = err.error?.detail || 'Errore nella creazione del colore';
          this.loading = false;
        }
      });
    }
  }

  deleteColor(id: string) {
        this.loading = true;
    this.supportService.deleteColor(id).subscribe({
      next: () => {
        this.successMessage = 'Colore eliminato con successo';
        this.loadColors();
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: (err) => {
        if (err.status === 409) {
          this.deleteMessage = err.error?.detail || 'Questo colore Ã¨ utilizzato in uno o piÃ¹ prodotti e non puÃ² essere eliminato. Puoi disattivarlo dalla visibilitÃ .';
        } else {
          this.errorMessage = err.error?.detail || 'Errore nell\'eliminazione del colore';
        }
        this.loading = false;
      }
    });
  }

  toggleVisibility(color: ColorDto) {
    const data: UpdateColorRequest = {
      name: color.name,
      htmlColor: color.htmlColor,
      isVisible: !color.isVisible
    };
    this.supportService.updateColor(color.id, data).subscribe({
      next: () => {
        this.successMessage = color.isVisible ? 'Colore nascosto' : 'Colore visibile';
        this.loadColors();
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
    this.deleteColor(id);
  }

  onDeleteCancelled(): void { this.confirmDeleteId = null; }
}



