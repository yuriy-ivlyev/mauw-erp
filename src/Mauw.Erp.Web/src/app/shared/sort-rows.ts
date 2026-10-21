export type SortValue = string | number | boolean | Date | null | undefined;

export function sortRows<T>(
  rows: readonly T[],
  valueSelector: (row: T) => SortValue,
  descending = false
): T[] {
  const collator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });
  return rows
    .map((row, index) => ({ row, index, value: valueSelector(row) }))
    .sort((left, right) => {
      let comparison: number;
      if (left.value == null || right.value == null) {
        comparison = left.value == null ? (right.value == null ? 0 : -1) : 1;
      } else if (left.value instanceof Date && right.value instanceof Date) {
        comparison = left.value.getTime() - right.value.getTime();
      } else if (typeof left.value === 'number' && typeof right.value === 'number') {
        comparison = left.value - right.value;
      } else if (typeof left.value === 'boolean' && typeof right.value === 'boolean') {
        comparison = Number(left.value) - Number(right.value);
      } else {
        comparison = collator.compare(String(left.value), String(right.value));
      }

      return (descending ? -comparison : comparison) || left.index - right.index;
    })
    .map(({ row }) => row);
}
