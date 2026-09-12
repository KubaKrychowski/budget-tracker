import { computed, signal } from '@angular/core';

/**
 * Stan panelu „od–do" na liczbach (`nzCustomFilter`) — używany dla kwot i liczby transakcji.
 *
 * Wersja robocza (`draftFrom`/`draftTo`, edytowana w polach) i zastosowana (prywatna, użyta
 * do filtrowania) są rozdzielone — filtr działa dopiero po kliknięciu „Szukaj", nie przy
 * każdej zmianie liczby. To wzorzec z oficjalnego przykładu NG-ZORRO dla custom filter panel
 * (`nz-filter-trigger` + własny `nz-dropdown-menu`), nie improwizacja.
 */
export class RangeFilterState {
  readonly visible = signal(false);
  readonly draftFrom = signal<number | null>(null);
  readonly draftTo = signal<number | null>(null);
  private readonly from = signal<number | null>(null);
  private readonly to = signal<number | null>(null);
  readonly active = computed(() => this.from() !== null || this.to() !== null);

  search(): void {
    this.from.set(this.draftFrom());
    this.to.set(this.draftTo());
    this.visible.set(false);
  }

  reset(): void {
    this.draftFrom.set(null);
    this.draftTo.set(null);
    this.from.set(null);
    this.to.set(null);
  }

  /** Granice WŁĄCZNIE; `null` po którejś stronie znaczy „bez tej granicy". */
  matches(value: number): boolean {
    const from = this.from();
    const to = this.to();
    if (from !== null && value < from) return false;
    if (to !== null && value > to) return false;
    return true;
  }
}
