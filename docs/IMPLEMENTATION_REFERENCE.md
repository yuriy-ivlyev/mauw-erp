# Product Variant Management - Key Implementation Details

## Component Architecture

### Data Flow

```
Component Init
  ↓
loadSupportData() → Load colors, sizes, categories, collections, materials
  ↓
loadProducts() → Load product list
  ↓
User Action (Create/Edit)
  ↓
Modal Opens with Tabs: [Details] [Variants*] [Gallery*]
  * = Only visible when editing
```

### Variant Creation Flow

```
User fills variant form
  ↓
validateVariantForm()
  ├─ Required fields check
  ├─ Barcode uniqueness check
  └─ If valid: createProductVariant(request)
  ↓
API: POST /v1/products/{productId}/variants
  ├─ Request body: {productId, colorId, sizeId, barcode, purchaseCost}
  └─ Response: ProductVariantDto with color/size info
  ↓
Add to productVariants array
  ↓
Reset form + Refresh table
```

## Key Methods Reference

### Support Data Loading
```typescript
private loadSupportData(): void {
  Promise.all([
    this.productService.getColors().toPromise(),
    this.productService.getSizes().toPromise(),
    this.productService.getCategories().toPromise(),
    this.productService.getCollections().toPromise(),
    this.productService.getMaterials().toPromise()
  ]).then(([colors, sizes, categories, collections, materials]) => {
    this.colors = colors || [];
    this.sizes = sizes || [];
    this.categories = categories || [];
    this.collections = collections || [];
    this.materials = materials || [];
  })
}
```

### Variant Addition with Validation
```typescript
addVariant(): void {
  // Validation
  if (!this.newVariantForm.colorId || !this.newVariantForm.sizeId || 
      !this.newVariantForm.barcode || !this.newVariantForm.purchaseCost) {
    this.variantError = 'Completa tutti i campi obbligatori';
    return;
  }
  
  // Barcode uniqueness check
  if (this.productVariants.some(v => v.barcode === this.newVariantForm.barcode)) {
    this.variantError = 'Barcode già esistente';
    return;
  }

  // API Call
  const request: CreateProductVariantRequest = {
    productId: this.editingProductId!,
    colorId: this.newVariantForm.colorId,
    sizeId: this.newVariantForm.sizeId,
    barcode: this.newVariantForm.barcode,
    purchaseCost: this.newVariantForm.purchaseCost
  };

  this.productService.createProductVariant(request).subscribe({
    next: (variant) => {
      this.productVariants.push(variant);
      this.newVariantForm = this.initializeVariantForm();
    },
    error: (err) => {
      this.variantError = err?.error || 'Errore nell\'aggiunta della variante';
    }
  });
}
```

### Color Indicator Display
```typescript
getColorHtml(colorId: string | undefined): string {
  if (!colorId) return '#cccccc';
  const color = this.colors.find(c => c.id === colorId);
  return color?.htmlColor || '#cccccc';
}

// Template usage:
// <div [style.backgroundColor]="getColorHtml(variant.colorId)"></div>
```

## Template Patterns

### Tab Navigation
```html
<button
  (click)="activeTab = 'details'"
  [class]="activeTab === 'details' ? 'border-blue-600 text-blue-600' : 'border-transparent'">
  Dettagli Prodotto
</button>

<button
  *ngIf="editingProductId"
  (click)="activeTab = 'variants'"
  [class]="activeTab === 'variants' ? 'border-blue-600 text-blue-600' : 'border-transparent'">
  Varianti
</button>
```

### Tab Content Switching
```html
<div *ngIf="activeTab === 'details'" class="space-y-4">
  <!-- Product form fields -->
</div>

<div *ngIf="activeTab === 'variants'" class="space-y-6">
  <!-- Variants table and add form -->
</div>

<div *ngIf="activeTab === 'gallery'" class="space-y-4">
  <!-- Image gallery -->
</div>
```

### Color Selector with Preview
```html
<select [(ngModel)]="newVariantForm.colorId">
  <option value="">-- Seleziona colore --</option>
  <option *ngFor="let color of colors" [value]="color.id">
    {{ color.name }}
  </option>
</select>

<!-- Live preview -->
<div *ngIf="newVariantForm.colorId" class="flex gap-2 items-center">
  <div class="w-6 h-6 rounded border" 
    [style.backgroundColor]="getColorHtml(newVariantForm.colorId)"></div>
  <span class="text-xs text-gray-600">{{ getColorName(newVariantForm.colorId) }}</span>
</div>
```

### Variants Table with Color Indicator
```html
<tr *ngFor="let variant of productVariants">
  <td>
    <div class="flex gap-2 items-center">
      <div class="w-5 h-5 rounded border" 
        [style.backgroundColor]="getColorHtml(variant.colorId)"></div>
      <span>{{ variant.color }}</span>
    </div>
  </td>
  <td>{{ variant.size }}</td>
  <td>{{ variant.barcode }}</td>
  <td>&euro;{{ variant.purchaseCost.toFixed(2) }}</td>
  <td>
    <button (click)="deleteVariant(variant.id)"
      [disabled]="deletingVariantId === variant.id">
      {{ deletingVariantId === variant.id ? 'Eliminando...' : 'Elimina' }}
    </button>
  </td>
</tr>
```

### Form Error Display
```html
<div *ngIf="formError" class="bg-red-100 text-red-800 p-3 rounded text-sm">
  {{ formError }}
</div>

<div *ngIf="variantError" class="bg-red-100 text-red-800 p-3 rounded text-sm">
  {{ variantError }}
</div>
```

## State Management

### Component Properties
```typescript
// Support data (loaded once on init)
colors: ColorDto[] = [];
sizes: SizeDto[] = [];
categories: CategoryDto[] = [];
collections: CollectionDto[] = [];
materials: MaterialDto[] = [];

// Modal state
showModal = false;
activeTab: 'details' | 'variants' | 'gallery' = 'details';
editingProductId: string | null = null;

// Variant management
productVariants: ProductVariantDto[] = [];
newVariantForm = { colorId: '', sizeId: '', barcode: '', purchaseCost: 0 };

// Loading/error states
loadingVariants = false;
savingVariant = false;
variantError: string | null = null;
deletingVariantId: string | null = null;
```

## API Contracts

### Variant Creation Request
```typescript
interface CreateProductVariantRequest {
  productId: string;      // Product ID
  colorId: string;        // Color ID from colors dropdown
  sizeId: string;         // Size ID from sizes dropdown
  barcode: string;        // Unique barcode
  purchaseCost: number;   // Purchase cost decimal
}
```

### Variant Response
```typescript
interface ProductVariantDto {
  id: string;
  productId: string;
  colorId?: string;
  sizeId?: string;
  size: string;           // Size name from size entity
  color: string;          // Color name from color entity
  barcode: string;
  purchaseCost: number;
  stockQuantity: number;
  listPrice: number;
  finalNetPrice: number;
  vatAmount: number;
  finalGrossPrice: number;
}
```

### Color Data
```typescript
interface ColorDto {
  id: string;
  name: string;           // "Red", "Blue", etc.
  htmlColor: string;      // "#FF0000", "#0000FF", etc.
}
```

## Error Handling Patterns

### Validation Errors (Client-side)
```typescript
// Variant form validation
if (!colorId || !sizeId || !barcode || !purchaseCost) {
  this.variantError = 'Completa tutti i campi obbligatori';
  return;
}

// Barcode uniqueness
if (this.productVariants.some(v => v.barcode === barcode)) {
  this.variantError = 'Barcode già esistente';
  return;
}
```

### API Errors (Server-side)
```typescript
error: (err) => {
  this.variantError = err?.error && typeof err.error === 'string'
    ? err.error
    : 'Errore nell\'aggiunta della variante';
  console.error(err);
}
```

## Performance Considerations

1. **Parallel Data Loading**: Support data loaded in parallel with Promise.all()
2. **Lazy Loading**: Variants loaded only when editing product
3. **Minimal Re-renders**: Tab content uses *ngIf for efficient DOM updates
4. **Service Caching**: Consider caching support data if API calls are frequent
5. **Unsubscribe**: Standalone component with OnDestroy can add explicit cleanup if needed

## Future Enhancements

- [ ] Variant editing (currently add/delete only)
- [ ] Bulk variant creation
- [ ] Import variants from CSV
- [ ] Variant pricing calculations
- [ ] Stock level management
- [ ] Material composition UI
- [ ] Drag-to-reorder variants
- [ ] Variant image management
- [ ] Variant filtering/search in table
