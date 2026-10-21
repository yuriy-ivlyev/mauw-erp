import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-pagination',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './pagination.component.html'
})
export class PaginationComponent implements OnChanges {
  @Input() page = 1;
  @Input() pageSize = 20;
  @Input() totalCount = 0;
  @Output() pageChange = new EventEmitter<number>();
  @Output() pageSizeChange = new EventEmitter<number>();

  requestedPage = 1;
  readonly pageSizes = [10, 20, 30, 50, 100];

  ngOnChanges(): void {
    this.requestedPage = this.page;
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get isRequestedPageValid(): boolean {
    return Number.isInteger(this.requestedPage)
      && this.requestedPage >= 1
      && this.requestedPage <= this.totalPages;
  }

  get firstItem(): number {
    return this.totalCount === 0 ? 0 : (this.page - 1) * this.pageSize + 1;
  }

  get lastItem(): number {
    return Math.min(this.page * this.pageSize, this.totalCount);
  }

  onPageInput(event: Event): void {
    this.requestedPage = Number((event.target as HTMLInputElement).value);
  }

  goToRequestedPage(): void {
    if (this.isRequestedPageValid) {
      this.pageChange.emit(this.requestedPage);
    }
  }

  setPageSize(pageSize: number): void {
    if (this.pageSizes.includes(pageSize)) {
      this.pageSizeChange.emit(pageSize);
    }
  }
}
