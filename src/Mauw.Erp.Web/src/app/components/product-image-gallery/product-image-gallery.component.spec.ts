import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ProductImageGalleryComponent } from './product-image-gallery.component';
import { ImageService } from '../../services/image.service';
import { ProductService } from '../../services/product.service';
import { ImageDto, ProductVariantDto } from '../../models/product.model';

describe('ProductImageGalleryComponent', () => {
  const image = (id: string, targetId: string, category: ImageDto['category'] = 'Zoom1', sortOrder = 1): ImageDto =>
    ({ id, targetId, url: `/api/images/${targetId}/${id}.jpg`, category, sortOrder });
  const variant = (id: string, barcode: string): ProductVariantDto =>
    ({ id, productId: 'p', barcode, color: 'Rosso', size: 'M' } as ProductVariantDto);

  let service: jasmine.SpyObj<ImageService>;
  let products: jasmine.SpyObj<ProductService>;
  let component: ProductImageGalleryComponent;
  let changed: number;

  beforeEach(() => {
    service = jasmine.createSpyObj<ImageService>('ImageService', ['list', 'upload', 'remove', 'setCategory']);
    products = jasmine.createSpyObj<ProductService>('ProductService', ['setVarianteVetrina']);
    service.list.and.returnValue(of([]));
    TestBed.configureTestingModule({
      imports: [ProductImageGalleryComponent],
      providers: [
        { provide: ImageService, useValue: service },
        { provide: ProductService, useValue: products }
      ]
    });
    component = TestBed.createComponent(ProductImageGalleryComponent).componentInstance;
    changed = 0;
    component.changed.subscribe(() => changed++);
  });

  function setInputs(inputs: { productId: string; hasVariants: boolean; variants: ProductVariantDto[] }): void {
    Object.assign(component, inputs);
    component.ngOnChanges({ productId: {} as any, hasVariants: {} as any, variants: {} as any });
  }

  it('uses the product as target when it has no variants', () => {
    setInputs({ productId: 'p', hasVariants: false, variants: [variant('v1', 'B1')] });

    expect(component.targetId).toBe('p');
    expect(service.list).toHaveBeenCalledOnceWith('p');
  });

  it('defaults to the first variant, loads its images and selects the main one', () => {
    service.list.and.returnValue(of([image('a', 'v1', 'Zoom1'), image('b', 'v1', 'Main', 0)]));

    setInputs({ productId: 'p', hasVariants: true, variants: [variant('v1', 'B1'), variant('v2', 'B2')] });

    expect(component.targetId).toBe('v1');
    expect(component.selected?.id).toBe('b');
  });

  it('does not use the product as target and loads nothing when a product with variants has none', () => {
    setInputs({ productId: 'p', hasVariants: true, variants: [] });

    expect(component.targetId).toBeNull();
    expect(service.list).not.toHaveBeenCalled();
  });

  it('keeps the chosen variant when the variants list is refreshed and switches images on selection', () => {
    setInputs({ productId: 'p', hasVariants: true, variants: [variant('v1', 'B1'), variant('v2', 'B2')] });
    component.selectTarget('v2');
    service.list.calls.reset();

    setInputs({ productId: 'p', hasVariants: true, variants: [variant('v1', 'B1'), variant('v2', 'B2'), variant('v3', 'B3')] });

    expect(component.targetId).toBe('v2');
    expect(service.list).not.toHaveBeenCalled();
  });

  it('uploads each file to the selected variant with the chosen category', () => {
    setInputs({ productId: 'p', hasVariants: true, variants: [variant('v1', 'B1'), variant('v2', 'B2')] });
    component.selectTarget('v2');
    component.category = 'OnPerson';
    service.upload.and.callFake((t, f) => of(image(f.name, t, 'OnPerson')));
    const files = [new File(['a'], 'a.jpg', { type: 'image/jpeg' }), new File(['b'], 'b.png', { type: 'image/png' })];

    component.onFilesSelected({ target: { files, value: '' } } as unknown as Event);

    expect(service.upload).toHaveBeenCalledTimes(2);
    expect(service.upload.calls.argsFor(0)).toEqual(['v2', files[0], 'OnPerson']);
    expect(component.images.length).toBe(2);
    expect(component.uploading).toBeFalse();
  });

  it('sends no category when automatic is chosen', () => {
    setInputs({ productId: 'p', hasVariants: false, variants: [] });
    service.upload.and.returnValue(of(image('a', 'p', 'Main', 0)));

    component.onFilesSelected({ target: { files: [new File(['a'], 'a.webp', { type: 'image/webp' })], value: '' } } as unknown as Event);

    expect(service.upload.calls.argsFor(0)[2]).toBeUndefined();
  });

  it('rejects unsupported or oversized files without calling the API', () => {
    setInputs({ productId: 'p', hasVariants: false, variants: [] });
    const big = new File([new ArrayBuffer(10 * 1024 * 1024 + 1)], 'big.jpg', { type: 'image/jpeg' });

    component.onFilesSelected({ target: { files: [new File(['x'], 'x.gif', { type: 'image/gif' })], value: '' } } as unknown as Event);
    expect(component.error).toContain('x.gif');
    component.onFilesSelected({ target: { files: [big], value: '' } } as unknown as Event);
    expect(component.error).toContain('big.jpg');
    expect(service.upload).not.toHaveBeenCalled();
  });

  it('shows the backend message when an upload is refused', () => {
    setInputs({ productId: 'p', hasVariants: false, variants: [] });
    service.upload.and.returnValue(throwError(() => ({ error: { error: 'Images of a product with variants belong to its variants.' } })));

    component.onFilesSelected({ target: { files: [new File(['a'], 'a.jpg', { type: 'image/jpeg' })], value: '' } } as unknown as Event);

    expect(component.error).toContain('belong to its variants');
    expect(component.uploading).toBeFalse();
  });

  it('deletes only after confirmation and selects another image', () => {
    const a = image('a', 'p', 'Main', 0);
    const b = image('b', 'p');
    service.list.and.returnValue(of([a, b]));
    service.remove.and.returnValue(of(undefined));
    setInputs({ productId: 'p', hasVariants: false, variants: [] });

    component.pendingDelete = a;
    expect(service.remove).not.toHaveBeenCalled();
    component.confirmDelete();

    expect(service.remove).toHaveBeenCalledOnceWith('p', 'a');
    expect(component.images).toEqual([b]);
    expect(component.selected).toBe(b);
    expect(component.pendingDelete).toBeNull();
    expect(changed).toBe(1);
  });

  it('offers every category except Main on upload and all of them for an existing image', () => {
    expect(component.uploadCategories).not.toContain('Main');
    expect(component.uploadCategories.length).toBe(7);
    expect(component.allCategories).toContain('Main');
  });

  it('changes the category of the selected image, reloads the list and keeps the selection', () => {
    const a = image('a', 'p', 'Main', 0);
    const b = image('b', 'p', 'Left', 1);
    service.list.and.returnValues(of([a, b]), of([image('a', 'p', 'Left', 0), image('b', 'p', 'Main', 1)]));
    service.setCategory.and.returnValue(of(image('b', 'p', 'Main', 1)));
    setInputs({ productId: 'p', hasVariants: false, variants: [] });
    component.selected = b;

    component.changeCategory('Main');

    expect(service.setCategory).toHaveBeenCalledOnceWith('p', 'b', 'Main');
    expect(service.list).toHaveBeenCalledTimes(2);
    expect(component.selected?.id).toBe('b');
    expect(component.images.find(i => i.id === 'a')?.category).toBe('Left');
    expect(changed).toBe(1);
  });

  it('does nothing when the category is unchanged and reloads after a refused change', () => {
    const a = image('a', 'p', 'Zoom1');
    service.list.and.returnValue(of([a]));
    setInputs({ productId: 'p', hasVariants: false, variants: [] });

    component.changeCategory('Zoom1');
    expect(service.setCategory).not.toHaveBeenCalled();

    service.setCategory.and.returnValue(throwError(() => ({ error: { error: 'Invalid image category.' } })));
    component.changeCategory('Left');
    expect(component.error).toContain('Invalid image category.');
    expect(component.updating).toBeFalse();
    expect(changed).toBe(0);
  });

  it('marks and unmarks the selected variant as vetrina and notifies the parent', () => {
    products.setVarianteVetrina.and.returnValue(of(undefined));
    setInputs({ productId: 'p', hasVariants: true, variants: [variant('v1', 'B1'), variant('v2', 'B2')] });
    component.selectTarget('v2');

    component.toggleVetrina({ target: { checked: true } } as unknown as Event);
    component.toggleVetrina({ target: { checked: false } } as unknown as Event);

    expect(products.setVarianteVetrina.calls.allArgs()).toEqual([['p', 'v2', true], ['p', 'v2', false]]);
    expect(changed).toBe(2);
  });

  it('reverts the checkbox and shows the error when vetrina cannot be set', () => {
    products.setVarianteVetrina.and.returnValue(throwError(() => ({ error: { error: 'Variant not found' } })));
    setInputs({ productId: 'p', hasVariants: true, variants: [variant('v1', 'B1')] });
    const checkbox = { checked: true };

    component.toggleVetrina({ target: checkbox } as unknown as Event);

    expect(checkbox.checked).toBeFalse();
    expect(component.error).toContain('Variant not found');
  });

  it('reflects the vetrina flag in the variant label and in isVetrina', () => {
    const flagged = { ...variant('v1', 'B1'), varianteVetrina: true };
    setInputs({ productId: 'p', hasVariants: true, variants: [flagged, variant('v2', 'B2')] });

    expect(component.isVetrina).toBeTrue();
    expect(component.variantLabel(flagged)).toContain('★');
    component.selectTarget('v2');
    expect(component.isVetrina).toBeFalse();
  });
});
