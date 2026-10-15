import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SupportService, MacroCategoryDto, CreateMacroCategoryRequest, UpdateMacroCategoryRequest } from '../../../services/support.service';

@Component({
  selector: 'app-macro-categorie',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './macro-categorie.component.html',
  styleUrl: './macro-categorie.component.scss'
})
export class MacroCategorieComponent implements OnInit {
  macroCategories: MacroCategoryDto[] = [];
  showForm = false;
  editingId: string | null = null;
  form!: FormGroup;
  errorMessage = '';
  successMessage = '';
  loading = false;
  deleteMessage = '';

  constructor(
    private supportService: SupportService,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(1)]]
    });
  }

  ngOnInit() {
    this.loadMacroCategories();
  }

  loadMacroCategories() {
    this.loading = true;
    this.supportService.getMacroCategories().subscribe({
      next: (data) => {
        this.macroCategories = data;
        this.loading = false;
      },
      error: (err) => {
        console.error('Errore caricamento macro categorie:', err);
        this.errorMessage = 'Errore nel caricamento delle macro categorie';
        this.loading = false;
      }
    });
  }

  openForm(macroCategory?: MacroCategoryDto) {
    if (macroCategory) {
      this.editingId = macroCategory.id;
      this.form.patchValue({
        name: macroCategory.name
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
      const data: UpdateMacroCategoryRequest = {
        name: this.form.get('name')!.value,
        isVisible: this.macroCategories.find(m => m.id === this.editingId)?.isVisible || true
      };
      this.supportService.updateMacroCategory(this.editingId, data).subscribe({
        next: () => {
          this.successMessage = 'Macro categoria aggiornata con successo';
          this.loadMacroCategories();
          this.closeForm();
          setTimeout(() => this.successMessage = '', 3000);
        },
        error: (err) => {
          this.errorMessage = err.error?.detail || 'Errore nell\'aggiornamento della macro categoria';
          this.loading = false;
        }
      });
    } else {
      const data: CreateMacroCategoryRequest = {
        name: this.form.get('name')!.value
      };
      this.supportService.createMacroCategory(data).subscribe({
        next: () => {
          this.successMessage = 'Macro categoria creata con successo';
          this.loadMacroCategories();
          this.closeForm();
          setTimeout(() => this.successMessage = '', 3000);
        },
        error: (err) => {
          this.errorMessage = err.error?.detail || 'Errore nella creazione della macro categoria';
          this.loading = false;
        }
      });
    }
  }

  deleteMacroCategory(id: string) {
    if (!confirm('Sei sicuro di voler eliminare questa macro categoria?')) return;

    this.loading = true;
    this.supportService.deleteMacroCategory(id).subscribe({
      next: () => {
        this.successMessage = 'Macro categoria eliminata con successo';
        this.loadMacroCategories();
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: (err) => {
        if (err.status === 409) {
          this.deleteMessage = err.error?.detail || 'Questa macro categoria è utilizzata in una o più categorie e non può essere eliminata. Puoi disattivarla dalla visibilità.';
        } else {
          this.errorMessage = err.error?.detail || 'Errore nell\'eliminazione della macro categoria';
        }
        this.loading = false;
      }
    });
  }

  toggleVisibility(macroCategory: MacroCategoryDto) {
    const data: UpdateMacroCategoryRequest = {
      name: macroCategory.name,
      isVisible: !macroCategory.isVisible
    };
    this.supportService.updateMacroCategory(macroCategory.id, data).subscribe({
      next: () => {
        this.successMessage = macroCategory.isVisible ? 'Macro categoria nascosta' : 'Macro categoria visibile';
        this.loadMacroCategories();
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: (err) => {
        this.errorMessage = 'Errore nell\'aggiornamento della visibilità';
      }
    });
  }
}
