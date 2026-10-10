import { StrategyVariant } from '../../core/api/models/strategies';
import {
  addVariant, duplicateVariant, MAX_VARIANT_NAME_LENGTH, MAX_VARIANTS, nameError, pruneNode, removeVariant, renameVariant,
  setNodeDisabled, uniqueName,
} from './strategy-variants';

const variant = (id: string, name: string, disabledNodeIds: string[] = []): StrategyVariant => ({ id, name, disabledNodeIds });

describe('strategy-variants', () => {
  it('uniqueName dodaje numer do zajętej nazwy bez względu na wielkość liter', () => {
    const variants = [variant('a', 'Bez podwyżki'), variant('b', 'bez podwyżki 2')];

    expect(uniqueName('Wariant', variants)).toBe('Wariant');
    expect(uniqueName('BEZ PODWYŻKI', variants)).toBe('BEZ PODWYŻKI 3');
  });

  it('nameError wymaga nazwy do 60 znaków, której nie używa inny wariant', () => {
    const variants = [variant('a', 'Bez podwyżki')];

    expect(nameError('   ', variants)).toBe('required');
    expect(nameError('x'.repeat(MAX_VARIANT_NAME_LENGTH + 1), variants)).toBe('tooLong');
    expect(nameError(' bez PODWYŻKI ', variants)).toBe('taken');
    // Wariant może zachować własną nazwę przy zmianie, np. samej wielkości liter.
    expect(nameError('BEZ PODWYŻKI', variants, 'a')).toBeNull();
    expect(nameError('Inny', variants)).toBeNull();
  });

  it('addVariant nie przekracza limitu wariantów', () => {
    const full = Array.from({ length: MAX_VARIANTS }, (_, i) => variant(`v${i}`, `W${i}`));

    expect(addVariant(full, variant('extra', 'Dodatkowy'))).toHaveLength(MAX_VARIANTS);
    expect(addVariant([], variant('a', 'A'))).toHaveLength(1);
  });

  it('duplicateVariant kopiuje wyłączenia i nadaje wolną nazwę z dopiskiem', () => {
    const variants = [variant('a', 'Bez podwyżki', ['raise'])];

    const copied = duplicateVariant(variants, 'a', 'b', 'kopia');
    const twice = duplicateVariant(copied, 'a', 'c', 'kopia');

    expect(copied[1]).toEqual({ id: 'b', name: 'Bez podwyżki (kopia)', disabledNodeIds: ['raise'] });
    expect(twice[2].name).toBe('Bez podwyżki (kopia) 2');
    // Kopia ma własną listę: zmiana w kopii nie rusza oryginału.
    expect(setNodeDisabled(copied, 'b', 'raise', false)[0].disabledNodeIds).toEqual(['raise']);
  });

  it('setNodeDisabled zmienia tylko wskazany wariant i nie powtarza kafelka', () => {
    const variants = [variant('a', 'A'), variant('b', 'B')];

    const once = setNodeDisabled(variants, 'a', 'n1', true);
    const twice = setNodeDisabled(once, 'a', 'n1', true);

    expect(twice[0].disabledNodeIds).toEqual(['n1']);
    expect(twice[1].disabledNodeIds).toEqual([]);
    expect(setNodeDisabled(twice, 'a', 'n1', false)[0].disabledNodeIds).toEqual([]);
  });

  it('pruneNode zdejmuje usunięty kafelek ze wszystkich wariantów', () => {
    // Łapie zapis, który serwer odrzuca (400), bo wariant wyłącza kafelek, którego już nie ma na tablicy.
    const variants = [variant('a', 'A', ['n1', 'n2']), variant('b', 'B', ['n1']), variant('c', 'C')];

    expect(pruneNode(variants, 'n1').map((v) => v.disabledNodeIds)).toEqual([['n2'], [], []]);
  });

  it('removeVariant i renameVariant działają na jednym wariancie', () => {
    const variants = [variant('a', 'A'), variant('b', 'B')];

    expect(removeVariant(variants, 'a').map((v) => v.id)).toEqual(['b']);
    expect(renameVariant(variants, 'b', '  Nowa ')[1].name).toBe('Nowa');
    expect(renameVariant(variants, 'b', 'Nowa')[0].name).toBe('A');
  });
});
