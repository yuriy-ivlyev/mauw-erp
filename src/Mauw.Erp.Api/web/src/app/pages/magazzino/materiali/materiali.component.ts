import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { SupportService, MaterialDto, CreateMaterialRequest, UpdateMaterialRequest } from '../../../services/support.service';

@Component({
  selector: 'app-materiali',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './materiali.component.html',
  styleUrl: './materiali.component.scss'
})
export class MmaterialiComponent implements OnInit {
  materials: MaterialDto[] = [];
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
    if (!confirm('Sei sicuro di voler eliminare questo materiale?')) return;

    this.loading = true;
    this.supportService.deleteMaterial(id).subscribe({
      next: () => {
        this.successMessage = 'Materiale eliminato con successo';
        this.loadMaterials();
        setTimeout(() => this.successMessage = '', 3000);
      },
      error: (err) => {
        if (err.status === 409) {
          this.deleteMessage = err.error?.detail || 'Questo materiale è utilizzato in uno o più prodotti e non può essere eliminato. Puoi disattivarlo dalla visibilità.';
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
        this.errorMessage = 'Errore nell\'aggiornamento della visibilità';
      }
    });
  }
}
