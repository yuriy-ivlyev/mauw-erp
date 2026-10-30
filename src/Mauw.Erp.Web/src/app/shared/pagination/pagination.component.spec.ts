import { PaginationComponent } from './pagination.component';
import { PageSlicePipe } from './page-slice.pipe';
import { SortHeaderDirective } from '../sort-header/sort-header.directive';
import { sortRows } from '../sort-rows';
import { TestBed } from '@angular/core/testing';

describe('shared list controls', () => {
  it('slices client-side rows for the requested page without mutating the source', () => {
    const rows = ['a', 'b', 'c', 'd'];
    const pipe = new PageSlicePipe();

    expect(pipe.transform(rows, 2, 2)).toEqual(['c', 'd']);
    expect(pipe.transform(rows, 3, 2)).toEqual([]);
    expect(rows).toEqual(['a', 'b', 'c', 'd']);
  });

  it('calculates the result range and page count', () => {
    const component = new PaginationComponent();
    component.page = 2;
    component.pageSize = 10;
    component.totalCount = 21;

    expect(component.totalPages).toBe(3);
    expect(component.firstItem).toBe(11);
    expect(component.lastItem).toBe(20);
  });

  it('keeps the visible page size and result range aligned at the default of 20', async () => {
    await TestBed.configureTestingModule({ imports: [PaginationComponent] }).compileComponents();
    const fixture = TestBed.createComponent(PaginationComponent);
    fixture.componentInstance.totalCount = 14907;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
    expect(select.selectedOptions[0]?.textContent?.trim()).toBe('20');
    expect(fixture.componentInstance.pageSize).toBe(20);
    expect(fixture.componentInstance.lastItem).toBe(20);
    expect(fixture.componentInstance.totalPages).toBe(746);
  });

  it('emits a supported page size and ignores unsupported values', () => {
    const component = new PaginationComponent();
    const sizes: number[] = [];
    component.pageSizeChange.subscribe(size => sizes.push(size));

    component.setPageSize(30);
    component.setPageSize(15);

    expect(sizes).toEqual([30]);
  });

  it('emits only valid manually selected pages', () => {
    const component = new PaginationComponent();
    component.totalCount = 25;
    component.pageSize = 10;
    const requestedPages: number[] = [];
    component.pageChange.subscribe(page => requestedPages.push(page));
    component.requestedPage = 3;
    component.goToRequestedPage();
    component.requestedPage = 4;
    component.goToRequestedPage();

    expect(requestedPages).toEqual([3]);
  });

  it('toggles sortable headers between ascending and descending', () => {
    const directive = new SortHeaderDirective();
    directive.sortKey = 'name';
    const changes: Array<{ sortBy: string; sortDescending: boolean }> = [];
    directive.sortChange.subscribe(change => changes.push(change));

    directive.onClick();
    directive.activeSortBy = 'name';
    directive.sortDescending = false;
    directive.onClick();

    expect(changes).toEqual([
      { sortBy: 'name', sortDescending: false },
      { sortBy: 'name', sortDescending: true }
    ]);
  });

  it('sorts string and numeric values without mutating source rows', () => {
    const rows = [{ name: 'prodotto 10' }, { name: 'Prodotto 2' }];

    const sorted = sortRows(rows, row => row.name);

    expect(sorted.map(row => row.name)).toEqual(['Prodotto 2', 'prodotto 10']);
    expect(rows[0].name).toBe('prodotto 10');
  });
});
