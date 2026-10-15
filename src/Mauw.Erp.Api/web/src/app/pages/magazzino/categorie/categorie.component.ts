import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SupportService, CategoryDto, CreateCategoryRequest, UpdateCategoryRequest, MacroCategoryDto } from '../../../services/support.service';

@Component({
  selector: 'app-categorie',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './categorie.component.html',
  styleUrl: './categorie.component.scss'
})
export class CategorieComponent implements OnInit {
  categories: CategoryDto[] = [];
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
      name: ['', [Validators.required, Validators.minLength(1)]],
      macroCategoryId: ['']
    });
  }

  ngOnInit() {
    this.loadMacroCategories();
    this.loadCategories();
  }

  loadMacroCategories() {
    this.supportService.getMacroCategories().subscribe({
      next: (data) => {
        this.macroCategories = data.filter(m => m.isVisible);
      },
      error: (err) => {
        console.error('Errore caricamento macro categorie:', err);
      }
    });
  }

  loadCategories() {
    this.loading = true;
    this.supportService.getCategories().subscribe({
      next: (data) => {
        this.categories = data;
        this.loading = false;
      },
      error: (err) => {
        console.error('Errore caricamento categorie:', err);
        this.errorMessage = 'Errore nel caricamento delle categorie';
        this.loading = false;
      }
    });
  }

  openForm(category?: CategoryDto) {
    if (category) {
      this.editingId = category.id;
      this.form.patchValue({
        name: category.name,
        macroCategoryId: category.macroCategoryId || ''
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
      const data: UpdateCategoryRequest = {
        name: this.form.get('name')!.value,
        macroCategoryId: this.form.get('macroCategoryId')!.value || undefined,
        isVisible: this.categories.find(c => c.id === this.editingId)?.isVisible || true
      };
      this.supportService.updateCategory(this.editingId, data).subscribe({
        next: () => {
          this.successMessage = 'Categoria aggiornata con successo';
          this.loadCategories();
          this.closeForm();
          setTimeout(() => this.successMessage = '', 3000);
        },
        error: (err) => {
          this.errorMessage = err.error?.detail || 'Errore nell\'aggiornamento della categoria';
          this.loading = false;
        }
      });
    } else {
      const data: CreateCategoryRequest = {
        name: this.form.get('name')!.value,
        macroCategoryId: this.form.get('macroCategoryId')!.value || undefined
      };
      this.supportService.createCategory(data).subscribe({
        next: () => {
          this.successMessage = 'Categoria creata con successo';
          this.loadCategories();
          this.closeForm();
          setTimeout(() => this.successMessage = '', 3000);
        },
        error: (err) => {
          this.errorMessage = err.error?.detail || 'Errore nella creazione della categoria';
          this.loading = false;
        }
      });
    }
  }

  deleteCategory(id: string) {
    if (!confirm('Sei sicuro di voler eliminare questa categoria?')) return;

    this.loading = true;
    this.supportService.deleteCategory(id).subscribe({
      next: () => {
        this.successMessage = 'Categoria eliminata con successo';
        this.loadCategories();
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: (err) => {
        if (err.status === 409) {
          this.deleteMessage = err.error?.detail || 'Questa categoria è utilizzata in uno o più prodotti e non può essere eliminata. Puoi disattivarla dalla visibilità.';
        } else {
          this.errorMessage = err.error?.detail || 'Errore nell\'eliminazione della categoria';
        }
        this.loading = false;
      }
    });
  }

  toggleVisibility(category: CategoryDto) {
    const data: UpdateCategoryRequest = {
      name: category.name,
      macroCategoryId: category.macroCategoryId,
      isVisible: !category.isVisible
    };
    this.supportService.updateCategory(category.id, data).subscribe({
      next: () => {
        this.successMessage = category.isVisible ? 'Categoria nascosta' : 'Categoria visibile';
        this.loadCategories();
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: (err) => {
        this.errorMessage = 'Errore nell\'aggiornamento della visibilità';
      }
    });
  }

  getMacroCategoryName(id?: string): string {
    if (!id) return '-';
    return this.macroCategories.find(m => m.id === id)?.name || '-';
  }
}
