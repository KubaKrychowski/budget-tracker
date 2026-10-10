import { StrategyVariant } from '../../core/api/models/strategies';

/** Ile wariantów (poza bazowym) może mieć strategia — ta sama granica co `Strategy.MaxVariants` w API. */
export const MAX_VARIANTS = 10;

/** Najdłuższa nazwa wariantu — ta sama co `StrategyGraphValidator.MaxVariantNameLength` w API. */
export const MAX_VARIANT_NAME_LENGTH = 60;

/** Dlaczego nazwa wariantu nie jest przyjmowana; `null` = w porządku. */
export type VariantNameError = 'required' | 'tooLong' | 'taken';

/**
 * Operacje na liście wariantów strategii. Wariant to tylko lista wyłączonych kafelków tego samego grafu, więc każda
 * operacja zwraca nową listę, a graf zostaje nietknięty. Czyste funkcje — bez sygnałów i HTTP — żeby reguły (unikalność
 * nazw, czyszczenie wyłączeń po usunięciu kafelka) dało się sprawdzić bez ekranu.
 */

/** Identyfikator wariantu nadawany po stronie klienta, jak identyfikatory kafelków. */
export function newVariantId(): string {
  return `v${Math.random().toString(36).slice(2, 10)}`;
}

const sameName = (a: string, b: string): boolean => a.trim().toLocaleLowerCase() === b.trim().toLocaleLowerCase();

/** Pierwsza wolna nazwa: `base`, a gdy zajęta — `base 2`, `base 3`… (wielkość liter nie ma znaczenia, jak w API). */
export function uniqueName(base: string, variants: readonly StrategyVariant[]): string {
  const taken = (name: string): boolean => variants.some((v) => sameName(v.name, name));
  if (!taken(base)) return base;
  let n = 2;
  while (taken(`${base} ${n}`)) n++;
  return `${base} ${n}`;
}

/** Sprawdza nazwę tak, jak zrobi to serwer; `ignoreId` pomija wariant, który właśnie zmieniamy. */
export function nameError(name: string, variants: readonly StrategyVariant[], ignoreId: string | null = null): VariantNameError | null {
  const trimmed = name.trim();
  if (!trimmed) return 'required';
  if (trimmed.length > MAX_VARIANT_NAME_LENGTH) return 'tooLong';
  return variants.some((v) => v.id !== ignoreId && sameName(v.name, trimmed)) ? 'taken' : null;
}

export function canAddVariant(variants: readonly StrategyVariant[]): boolean {
  return variants.length < MAX_VARIANTS;
}

export function addVariant(variants: readonly StrategyVariant[], variant: StrategyVariant): StrategyVariant[] {
  return canAddVariant(variants) ? [...variants, variant] : [...variants];
}

/** Kopia wariantu z tymi samymi wyłączeniami i wolną nazwą „<nazwa> (kopia)”. */
export function duplicateVariant(
  variants: readonly StrategyVariant[], sourceId: string, id: string, copySuffix: string,
): StrategyVariant[] {
  const source = variants.find((v) => v.id === sourceId);
  if (!source || !canAddVariant(variants)) return [...variants];
  const base = `${source.name} (${copySuffix})`.slice(0, MAX_VARIANT_NAME_LENGTH);
  return [...variants, { id, name: uniqueName(base, variants), disabledNodeIds: [...source.disabledNodeIds] }];
}

export function removeVariant(variants: readonly StrategyVariant[], id: string): StrategyVariant[] {
  return variants.filter((v) => v.id !== id);
}

export function renameVariant(variants: readonly StrategyVariant[], id: string, name: string): StrategyVariant[] {
  return variants.map((v) => (v.id === id ? { ...v, name: name.trim() } : v));
}

/** Włącza albo wyłącza kafelek w jednym wariancie; pozostałe warianty zostają bez zmian. */
export function setNodeDisabled(
  variants: readonly StrategyVariant[], variantId: string, nodeId: string, disabled: boolean,
): StrategyVariant[] {
  return variants.map((v) => {
    if (v.id !== variantId) return v;
    const rest = v.disabledNodeIds.filter((id) => id !== nodeId);
    return { ...v, disabledNodeIds: disabled ? [...rest, nodeId] : rest };
  });
}

/**
 * Zdejmuje usunięty kafelek z wyłączeń wszystkich wariantów.
 *
 * ⚠️ Serwer odrzuca (400) wariant, który wyłącza nieistniejący kafelek — bez tego zapis po usunięciu kafelka, który ktoś
 * wyłączył w wariancie, kończyłby się błędem, którego nie widać na tablicy.
 */
export function pruneNode(variants: readonly StrategyVariant[], nodeId: string): StrategyVariant[] {
  return variants.map((v) => (v.disabledNodeIds.includes(nodeId)
    ? { ...v, disabledNodeIds: v.disabledNodeIds.filter((id) => id !== nodeId) }
    : v));
}
