import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmDeleteModalComponent } from '../../../shared/confirm-delete-modal/confirm-delete-modal.component';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { SupportService, CategoryDto, CreateCategoryRequest, UpdateCategoryRequest, MacroCategoryDto } from '../../../services/support.service';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { SortHeaderDirective } from '../../../shared/sort-header/sort-header.directive';
import { SortChange } from '../../../models/pagination.model';
import { sortRows } from '../../../shared/sort-rows';

@Component({
  selector: 'app-categorie',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ConfirmDeleteModalComponent, PaginationComponent, SortHeaderDirective],
  templateUrl: './categorie.component.html',
  styleUrl: './categorie.component.scss'
})
export class CategorieComponent implements OnInit {
  categories: CategoryDto[] = [];
  macroCategories: MacroCategoryDto[] = [];
  page = 1;
  pageSize = 20;
  sortBy = 'name';
  sortDescending = false;
  showForm = false;
  editingId: string | null = null;
  form!: FormGroup;
  errorMessage = '';
  successMessage = '';
  loading = false;
  deleteMessage = '';
  confirmDeleteId: string | null = null;

  get pageItems(): CategoryDto[] {
    const sorted = sortRows(this.categories, category => {
      if (this.sortBy === 'id') return category.id;
      if (this.sortBy === 'macroCategory') return this.getMacroCategoryName(category.macroCategoryId);
      if (this.sortBy === 'visibility') return category.isVisible;
      return category.name;
    }, this.sortDescending);
    return sorted.slice((this.page - 1) * this.pageSize, this.page * this.pageSize);
  }

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
        this.loading = true;
    this.supportService.deleteCategory(id).subscribe({
      next: () => {
        this.successMessage = 'Categoria eliminata con successo';
        this.loadCategories();
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: (err) => {
        if (err.status === 409) {
          this.deleteMessage = err.error?.detail || 'Questa categoria Ã¨ utilizzata in uno o piÃ¹ prodotti e non puÃ² essere eliminata. Puoi disattivarla dalla visibilitÃ .';
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
        this.errorMessage = 'Errore nell\'aggiornamento della visibilitÃ ';
      }
    });
  }

  getMacroCategoryName(id?: string): string {
    if (!id) return '-';
    return this.macroCategories.find(m => m.id === id)?.name || '-';
  }

  goToPage(page: number): void {
    const totalPages = Math.max(1, Math.ceil(this.categories.length / this.pageSize));
    if (page >= 1 && page <= totalPages) this.page = page;
  }

  onPageSizeChange(pageSize: number): void {
    this.pageSize = pageSize;
    this.page = 1;
  }

  onSortChange(sort: SortChange): void {
    this.sortBy = sort.sortBy;
    this.sortDescending = sort.sortDescending;
    this.page = 1;
  }


  askDelete(id: string): void { this.confirmDeleteId = id; }

  onDeleteConfirmed(): void {
    if (!this.confirmDeleteId) return;
    const id = this.confirmDeleteId;
    this.confirmDeleteId = null;
    this.deleteCategory(id);
  }

  onDeleteCancelled(): void { this.confirmDeleteId = null; }
}
