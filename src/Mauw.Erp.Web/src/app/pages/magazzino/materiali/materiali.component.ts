import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmDeleteModalComponent } from '../../../shared/confirm-delete-modal/confirm-delete-modal.component';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { SupportService, MaterialDto, CreateMaterialRequest, UpdateMaterialRequest } from '../../../services/support.service';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { SortHeaderDirective } from '../../../shared/sort-header/sort-header.directive';
import { SortChange } from '../../../models/pagination.model';
import { sortRows } from '../../../shared/sort-rows';

@Component({
  selector: 'app-materiali',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ConfirmDeleteModalComponent, PaginationComponent, SortHeaderDirective],
  templateUrl: './materiali.component.html',
  styleUrl: './materiali.component.scss'
})
export class MmaterialiComponent implements OnInit {
  materials: MaterialDto[] = [];
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

  get pageItems(): MaterialDto[] {
    return sortRows(this.materials, item => {
      if (this.sortBy === 'id') return item.id;
      if (this.sortBy === 'composition') return item.composition.length;
      if (this.sortBy === 'visibility') return item.isVisible;
      return item.name;
    }, this.sortDescending).slice((this.page - 1) * this.pageSize, this.page * this.pageSize);
  }

  constructor(
    private supportService: SupportService,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(1)]],
      composition: this.fb.array([])
    });
  }

  ngOnInit() {
    this.loadMaterials();
  }

  loadMaterials() {
    this.loading = true;
    this.supportService.getMaterials().subscribe({
      next: (data) => {
        this.materials = data;
        this.loading = false;
      },
      error: (err) => {
        console.error('Errore caricamento materiali:', err);
        this.errorMessage = 'Errore nel caricamento dei materiali';
        this.loading = false;
      }
    });
  }

  get composition(): FormArray {
    return this.form.get('composition') as FormArray;
  }

  addComposition() {
    const compForm = this.fb.group({
      componentName: ['', Validators.required],
      percentageValue: [0, [Validators.required, Validators.min(0), Validators.max(100)]]
    });
    this.composition.push(compForm);
  }

  removeComposition(index: number) {
    this.composition.removeAt(index);
  }

  openForm(material?: MaterialDto) {
    if (material) {
      this.editingId = material.id;
      this.form.patchValue({
        name: material.name
      });
      this.composition.clear();
      material.composition.forEach(comp => {
        this.composition.push(this.fb.group({
          componentName: [comp.componentName, Validators.required],
          percentageValue: [comp.percentageValue, [Validators.required, Validators.min(0), Validators.max(100)]]
        }));
      });
    } else {
      this.editingId = null;
      this.form.reset({ name: '' });
      this.composition.clear();
    }
    this.showForm = true;
    this.errorMessage = '';
    this.deleteMessage = '';
  }

  closeForm() {
    this.showForm = false;
    this.editingId = null;
    this.form.reset({ name: '' });
    this.composition.clear();
    this.errorMessage = '';
  }

  onSubmit() {
    if (!this.form.valid) return;

    this.loading = true;
    if (this.editingId) {
      const data: UpdateMaterialRequest = {
        name: this.form.get('name')!.value,
        composition: this.composition.value.length > 0 ? this.composition.value : undefined,
        isVisible: this.materials.find(m => m.id === this.editingId)?.isVisible || true
      };
      this.supportService.updateMaterial(this.editingId, data).subscribe({
        next: () => {
          this.successMessage = 'Materiale aggiornato con successo';
          this.loadMaterials();
          this.closeForm();
          setTimeout(() => this.successMessage = '', 3000);
        },
        error: (err) => {
          this.errorMessage = err.error?.detail || 'Errore nell\'aggiornamento del materiale';
          this.loading = false;
        }
      });
    } else {
      const data: CreateMaterialRequest = {
        name: this.form.get('name')!.value,
        composition: this.composition.value.length > 0 ? this.composition.value : undefined
      };
      this.supportService.createMaterial(data).subscribe({
        next: () => {
          this.successMessage = 'Materiale creato con successo';
          this.loadMaterials();
          this.closeForm();
          setTimeout(() => this.successMessage = '', 3000);
        },
        error: (err) => {
          this.errorMessage = err.error?.detail || 'Errore nella creazione del materiale';
          this.loading = false;
        }
      });
    }
  }

  deleteMaterial(id: string) {
        this.loading = true;
    this.supportService.deleteMaterial(id).subscribe({
      next: () => {
        this.successMessage = 'Materiale eliminato con successo';
        this.loadMaterials();
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: (err) => {
        if (err.status === 409) {
          this.deleteMessage = err.error?.detail || 'Questo materiale Ã¨ utilizzato in uno o piÃ¹ prodotti e non puÃ² essere eliminato. Puoi disattivarlo dalla visibilitÃ .';
        } else {
          this.errorMessage = err.error?.detail || 'Errore nell\'eliminazione del materiale';
        }
        this.loading = false;
      }
    });
  }

  toggleVisibility(material: MaterialDto) {
    const data: UpdateMaterialRequest = {
      name: material.name,
      composition: material.composition.map(c => ({
        componentName: c.componentName,
        percentageValue: c.percentageValue
      })),
      isVisible: !material.isVisible
    };
    this.supportService.updateMaterial(material.id, data).subscribe({
      next: () => {
        this.successMessage = material.isVisible ? 'Materiale nascosto' : 'Materiale visibile';
        this.loadMaterials();
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
    this.deleteMaterial(id);
  }

  onDeleteCancelled(): void { this.confirmDeleteId = null; }

  goToPage(page: number): void {
    if (page >= 1 && page <= Math.max(1, Math.ceil(this.materials.length / this.pageSize))) this.page = page;
  }

  onPageSizeChange(pageSize: number): void { this.pageSize = pageSize; this.page = 1; }

  onSortChange(sort: SortChange): void {
    this.sortBy = sort.sortBy;
    this.sortDescending = sort.sortDescending;
    this.page = 1;
  }
}


