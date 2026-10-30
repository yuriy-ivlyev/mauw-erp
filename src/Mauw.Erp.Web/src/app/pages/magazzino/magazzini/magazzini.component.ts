import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ConfirmDeleteModalComponent } from '../../../shared/confirm-delete-modal/confirm-delete-modal.component';
import { WarehouseService } from '../../../services/warehouse.service';
import { CompanySettingsService } from '../../../services/company-settings.service';
import {
  WarehouseSummary,
  WarehouseDetail,
  WarehouseType,
  WAREHOUSE_TYPE_LABELS,
  CreateWarehouseRequest,
  UpdateWarehouseRequest
} from '../../../models/warehouse.model';
import { CompanyAddressSite } from '../../../models/company-settings.model';
import { PaginationComponent } from '../../../shared/pagination/pagination.component';
import { SortHeaderDirective } from '../../../shared/sort-header/sort-header.directive';
import { SortChange } from '../../../models/pagination.model';
import { TableActionButtonComponent } from '../../../shared/table-action-button/table-action-button.component';
import { AddActionButtonComponent } from '../../../shared/add-action-button/add-action-button.component';

@Component({
  selector: 'app-magazzini',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, ConfirmDeleteModalComponent, PaginationComponent,
    SortHeaderDirective, TableActionButtonComponent, AddActionButtonComponent
  ],
  templateUrl: './magazzini.component.html',
  styleUrl: './magazzini.component.scss'
})
export class MagazziniComponent implements OnInit {
  warehouses: WarehouseSummary[] = [];
  operationalSites: CompanyAddressSite[] = [];

  // Pagination & filters
  page = 1;
  pageSize = 20;
  sortBy = 'code';
  sortDescending = false;
  totalCount = 0;
  totalPages = 0;
  searchTerm = '';
  filterType: number | '' = '';
  filterActive: '' | 'true' | 'false' = '';

  // UI state
  loading = false;
  showForm = false;
  editingId: string | null = null;
  successMessage = '';
  errorMessage = '';
  confirmDeleteId: string | null = null;
  confirmSetDefaultId: string | null = null;
  showSetDefaultConfirm = false;

  form!: FormGroup;

  readonly warehouseTypes = [
    { value: WarehouseType.Standard,      label: WAREHOUSE_TYPE_LABELS[WarehouseType.Standard] },
    { value: WarehouseType.Defective,     label: WAREHOUSE_TYPE_LABELS[WarehouseType.Defective] },
    { value: WarehouseType.Consignment,   label: WAREHOUSE_TYPE_LABELS[WarehouseType.Consignment] },
    { value: WarehouseType.Subcontractor, label: WAREHOUSE_TYPE_LABELS[WarehouseType.Subcontractor] },
    { value: WarehouseType.Transit,       label: WAREHOUSE_TYPE_LABELS[WarehouseType.Transit] },
  ];

  constructor(
    private warehouseService: WarehouseService,
    private companyService: CompanySettingsService,
    private fb: FormBuilder
  ) {
    this.form = this.fb.group({
      code:               ['', [Validators.required, Validators.maxLength(20), Validators.pattern(/^[A-Z0-9\-_]+$/)]],
      name:               ['', [Validators.required, Validators.maxLength(200)]],
      type:               [WarehouseType.Standard, Validators.required],
      contactAddressId:   ['', Validators.required],
      isSellable:         [true],
      isDefault:          [false],
      description:        [''],
    });

    // Auto-set isSellable based on type
    this.form.get('type')?.valueChanges.subscribe(type => {
      const numType = Number(type);
      if (numType === WarehouseType.Defective || numType === WarehouseType.Subcontractor) {
        this.form.get('isSellable')?.setValue(false);
      } else if (numType === WarehouseType.Standard) {
        this.form.get('isSellable')?.setValue(true);
      }
    });

    // Auto-uppercase code
    this.form.get('code')?.valueChanges.subscribe(val => {
      if (val && val !== val.toUpperCase()) {
        this.form.get('code')?.setValue(val.toUpperCase(), { emitEvent: false });
      }
    });
  }

  ngOnInit(): void {
    this.loadSites();
    this.load();
  }

  loadSites(): void {
    this.companyService.getCompany().subscribe({
      next: company => {
        const all: CompanyAddressSite[] = [];
        if (company.legalHeadquarters) all.push(company.legalHeadquarters);
        all.push(...(company.operationalSites ?? []));
        this.operationalSites = all;
      },
      error: () => {} // non blocca il caricamento
    });
  }

  load(): void {
    this.loading = true;
    this.warehouseService.getList({
      page: this.page,
      pageSize: this.pageSize,
      search: this.searchTerm || undefined,
      type: this.filterType !== '' ? Number(this.filterType) : undefined,
      isActive: this.filterActive !== '' ? this.filterActive === 'true' : undefined,
      sortBy: this.sortBy,
      sortDescending: this.sortDescending
    }).subscribe({
      next: result => {
        this.warehouses = result.items;
        this.totalCount = result.totalCount;
        this.totalPages = result.totalPages;
        if (this.totalPages > 0 && this.page > this.totalPages) {
          this.page = this.totalPages;
          this.load();
          return;
        }
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Errore nel caricamento dei magazzini.';
        this.loading = false;
      }
    });
  }

  onSearch(): void {
    this.page = 1;
    this.load();
  }

  onFilterChange(): void {
    this.page = 1;
    this.load();
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages && page !== this.page) {
      this.page = page;
      this.load();
    }
  }

  onPageSizeChange(pageSize: number): void {
    this.pageSize = pageSize;
    this.page = 1;
    this.load();
  }

  onSortChange(sort: SortChange): void {
    this.sortBy = sort.sortBy;
    this.sortDescending = sort.sortDescending;
    this.page = 1;
    this.load();
  }

  openNew(): void {
    this.editingId = null;
    this.form.reset({
      code: '', name: '', type: WarehouseType.Standard,
      contactAddressId: this.operationalSites[0]?.id ?? '',
      isSellable: true, isDefault: false, description: ''
    });
    this.form.get('code')?.enable();
    this.errorMessage = '';
    this.showForm = true;
  }

  openEdit(id: string): void {
    this.warehouseService.getById(id).subscribe({
      next: (w: WarehouseDetail) => {
        this.editingId = id;
        this.form.patchValue({
          code:             w.code,
          name:             w.name,
          type:             w.typeValue,
          contactAddressId: w.contactAddressId,
          isSellable:       w.isSellable,
          isDefault:        w.isDefault,
          description:      w.description ?? ''
        });
        this.form.get('code')?.disable(); // code is immutable after creation
        this.errorMessage = '';
        this.showForm = true;
      },
      error: () => { this.errorMessage = 'Errore nel caricamento del magazzino.'; }
    });
  }

  closeForm(): void {
    this.showForm = false;
    this.editingId = null;
    this.errorMessage = '';
  }

  onSubmit(): void {
    if (this.form.invalid) return;
    this.loading = true;

    if (this.editingId) {
      const req: UpdateWarehouseRequest = {
        name:             this.form.get('name')!.value,
        type:             Number(this.form.get('type')!.value),
        contactAddressId: this.form.get('contactAddressId')!.value,
        isSellable:       this.form.get('isSellable')!.value,
        isActive:         true,
        description:      this.form.get('description')!.value || null,
      };
      this.warehouseService.update(this.editingId, req).subscribe({
        next: () => { this.onSaveSuccess('Magazzino aggiornato con successo.'); },
        error: err => { this.onSaveError(err); }
      });
    } else {
      const req: CreateWarehouseRequest = {
        code:             this.form.get('code')!.value,
        name:             this.form.get('name')!.value,
        type:             Number(this.form.get('type')!.value),
        contactAddressId: this.form.get('contactAddressId')!.value,
        isSellable:       this.form.get('isSellable')!.value,
        isDefault:        this.form.get('isDefault')!.value,
        description:      this.form.get('description')!.value || null,
      };
      this.warehouseService.create(req).subscribe({
        next: () => { this.onSaveSuccess('Magazzino creato con successo.'); },
        error: err => { this.onSaveError(err); }
      });
    }
  }

  private onSaveSuccess(msg: string): void {
    this.successMessage = msg;
    this.closeForm();
    this.load();
    setTimeout(() => this.successMessage = '', 4000);
  }

  private onSaveError(err: any): void {
    this.errorMessage = err.error ?? err.message ?? 'Errore nel salvataggio.';
    this.loading = false;
  }

  askDelete(id: string): void {
    this.confirmDeleteId = id;
  }

  onDeleteConfirmed(): void {
    if (!this.confirmDeleteId) return;
    const id = this.confirmDeleteId;
    this.confirmDeleteId = null;
    this.warehouseService.delete(id).subscribe({
      next: () => {
        this.successMessage = 'Magazzino eliminato.';
        this.load();
        setTimeout(() => this.successMessage = '', 4000);
      },
      error: err => {
        this.errorMessage = err.error ?? 'Impossibile eliminare il magazzino.';
      }
    });
  }

  onDeleteCancelled(): void {
    this.confirmDeleteId = null;
  }

  askSetDefault(id: string): void {
    this.confirmSetDefaultId = id;
    this.showSetDefaultConfirm = true;
  }

  onSetDefaultConfirmed(): void {
    if (!this.confirmSetDefaultId) return;
    const id = this.confirmSetDefaultId;
    this.confirmSetDefaultId = null;
    this.showSetDefaultConfirm = false;
    this.warehouseService.setDefault(id).subscribe({
      next: () => {
        this.successMessage = 'Magazzino impostato come predefinito.';
        this.load();
        setTimeout(() => this.successMessage = '', 4000);
      },
      error: err => {
        this.errorMessage = err.error ?? 'Errore nell\'impostazione del predefinito.';
      }
    });
  }

  onSetDefaultCancelled(): void {
    this.confirmSetDefaultId = null;
    this.showSetDefaultConfirm = false;
  }

  getTypeBadgeClass(typeValue: number): string {
    switch (typeValue) {
      case WarehouseType.Standard:      return 'bg-green-100 text-green-800';
      case WarehouseType.Defective:     return 'bg-orange-100 text-orange-800';
      case WarehouseType.Consignment:   return 'bg-purple-100 text-purple-800';
      case WarehouseType.Subcontractor: return 'bg-blue-100 text-blue-800';
      case WarehouseType.Transit:       return 'bg-yellow-100 text-yellow-800';
      default:                          return 'bg-gray-100 text-gray-800';
    }
  }

  getSiteLabel(site: CompanyAddressSite): string {
    return site.siteName ? `${site.siteName} – ${site.city}` : site.city;
  }
}
