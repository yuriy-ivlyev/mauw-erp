import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'pageSlice',
  standalone: true,
  pure: true
})
export class PageSlicePipe implements PipeTransform {
  transform<T>(items: readonly T[] | null | undefined, page: number, pageSize: number): T[] {
    if (!items?.length || !Number.isInteger(page) || !Number.isInteger(pageSize) || page < 1 || pageSize < 1) {
      return [];
    }

    const start = (page - 1) * pageSize;
    return items.slice(start, start + pageSize);
  }
}
