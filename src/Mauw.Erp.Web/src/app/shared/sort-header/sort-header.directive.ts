import { Directive, EventEmitter, HostBinding, HostListener, Input, Output } from '@angular/core';
import { SortChange } from '../../models/pagination.model';

@Directive({
  selector: 'th[appSortHeader]',
  standalone: true
})
export class SortHeaderDirective {
  @Input({ alias: 'appSortHeader', required: true }) sortKey!: string;
  @Input() activeSortBy = '';
  @Input() sortDescending = false;
  @Output() sortChange = new EventEmitter<SortChange>();

  @HostBinding('class.sortable-column') sortableClass = true;
  @HostBinding('attr.tabindex') tabIndex = 0;

  @HostBinding('attr.aria-sort')
  get ariaSort(): 'ascending' | 'descending' | 'none' {
    if (this.activeSortBy !== this.sortKey) return 'none';
    return this.sortDescending ? 'descending' : 'ascending';
  }

  @HostBinding('attr.data-sort-indicator')
  get sortIndicator(): string {
    if (this.activeSortBy !== this.sortKey) return '↕';
    return this.sortDescending ? '▼' : '▲';
  }

  @HostListener('click')
  onClick(): void {
    this.toggleSort();
  }

  @HostListener('keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.toggleSort();
    }
  }

  private toggleSort(): void {
    this.sortChange.emit({
      sortBy: this.sortKey,
      sortDescending: this.activeSortBy === this.sortKey ? !this.sortDescending : false
    });
  }
}
