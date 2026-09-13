import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { describe, expect, it, beforeEach } from 'vitest';
import { APP_ICONS } from '../../../core/icons';
import { ConfirmDialogService } from '../../../core/confirm-dialog/confirm-dialog.service';
import { ConfirmDialogOptions } from '../../../core/confirm-dialog/confirm-dialog-options';
import { CategoryRule } from '../../../core/api/models/category-rule';
import { RulePreview } from '../../../core/api/models/rule-preview';
import { Rules } from './rules';

class FakeConfirmDialogService {
  lastOptions: ConfirmDialogOptions | null = null;
  private resolve: ((value: boolean) => void) | null = null;

  confirm(options: ConfirmDialogOptions): Promise<boolean> {
    this.lastOptions = options;
    return new Promise((resolve) => { this.resolve = resolve; });
  }

  respond(confirmed: boolean): void {
    this.resolve?.(confirmed);
    this.resolve = null;
  }
}

/** Dostęp do składowych `protected` — test sprawdza zachowanie, nie układ szablonu. */
interface RulesInternals {
  openCreate(): void;
  patch(key: string, value: unknown): void;
  runPreview(): Promise<void>;
  remove(rule: CategoryRule): Promise<void>;
  draftError(): string | null;
  preview(): RulePreview | null;
}

/**
 * Zakładka „Reguły predykatu". Testujemy to, czego nie widać w kodzie szablonu, a co decyduje
 * o poprawności: kolejność stosowania reguł, ostrzeżenie o remisie priorytetów, bramki zapisu
 * i to, że podgląd mówi o przesłonięciu, zamiast obiecywać skutek, którego reguła nie ma.
 */
describe('Rules', () => {
  let fixture: ComponentFixture<Rules>;
  let http: HttpTestingController;
  let confirmDialog: FakeConfirmDialogService;
  let internals: RulesInternals;

  const rule = (over: Partial<CategoryRule> = {}): CategoryRule => ({
    id: crypto.randomUUID(),
    pattern: 'STACJA PALIW',
    transactionTypePattern: null,
    direction: 'Expense',
    categoryId: crypto.randomUUID(),
    categoryName: 'Paliwo',
    priority: 100,
    minAmount: null,
    maxAmount: null,
    note: null,
    ...over,
  });

  const text = (): string => (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');

  /**
   * ⚠️ Treść modala NIE jest w `fixture.nativeElement`. `nz-modal` renderuje się w overlayu
   * dopiętym do `document.body`, więc asercje na zawartości kreatora muszą czytać dokument.
   * Bez tego test przechodziłby lub padał zależnie od tego, co akurat jest w tabeli.
   */
  const modalText = (): string => (document.body.textContent ?? '').replace(/\s+/g, ' ');

  /** Klik w przycisk po jego tekście — tak, jak zrobiłby to człowiek. */
  const clickButton = (label: string): void => {
    const button = [...fixture.nativeElement.querySelectorAll('button')]
      .find((b) => (b as HTMLElement).textContent?.trim() === label) as HTMLButtonElement | undefined;
    if (!button) throw new Error(`Brak przycisku „${label}"`);
    button.click();
    fixture.detectChanges();
  };

  /** Druga wizyta na ekranie: nowy komponent, ten sam localStorage. */
  const recreate = async (rules: CategoryRule[]): Promise<void> => {
    fixture.destroy();
    fixture = TestBed.createComponent(Rules);
    internals = fixture.componentInstance as unknown as RulesInternals;
    await settle(rules);
  };

  const settle = async (rules: CategoryRule[]): Promise<void> => {
    fixture.detectChanges();
    http.match('/api/categorization/rules').forEach((r) => r.flush(rules));
    http.match('/api/categories').forEach((r) => r.flush([{ id: 'cat-1', name: 'Paliwo' }]));
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    localStorage.clear();
    confirmDialog = new FakeConfirmDialogService();

    await TestBed.configureTestingModule({
      imports: [Rules],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideTranslateService(),
        provideNzIcons(APP_ICONS),
        provideNzI18n(pl_PL),
        { provide: ConfirmDialogService, useValue: confirmDialog },
      ],
    }).compileComponents();

    TestBed.inject(TranslateService).setTranslation('pl', {
      settings: {
        rules: {
          intro: 'Reguły przypisują kategorię.',
          listTitle: 'Lista reguł',
          create: 'Nowa reguła',
          empty: 'Nie ma jeszcze żadnej reguły.',
          tiedTitle: 'Reguły o tym samym priorytecie: {{priorities}}',
          tiedDescription: 'Przy remisie o wyniku decyduje kolejność dodania reguły.',
          tiedAcknowledge: 'Rozumiem',
          none: '—',
          columns: {
            priority: 'Priorytet', pattern: 'Wzorzec opisu', typePattern: 'Typ operacji',
            direction: 'Kierunek', category: 'Kategoria', amountRange: 'Widełki kwot',
            note: 'Notatka', actions: 'Akcje',
          },
          direction: { Any: 'Dowolny', Expense: 'Wydatek', Income: 'Przychód' },
          editor: {
            createTitle: 'Nowa reguła', editTitle: 'Edycja reguły',
            patternRequired: 'Wypełnij wzorzec opisu albo wzorzec typu operacji.',
            rangeInvalid: 'Kwota „do” musi być większa niż „od”.',
            categoryRequired: 'Wybierz kategorię.',
          },
          preview: {
            summary: 'Pasuje {{count}} z {{scanned}} transakcji.',
            shadowed: 'Z tego {{count}} zabiera reguła o wcześniejszym priorytecie.',
            noMatches: 'Żadna z {{scanned}} transakcji nie pasuje.',
          },
          delete: {
            body: 'Reguła „{{rule}}” → {{category}} (priorytet {{priority}}) zniknie z listy.',
            note: 'Transakcje, które już dostały kategorię z tej reguły, zostają bez zmian.',
            confirm: 'Usuń regułę',
          },
        },
      },
    });
    TestBed.inject(TranslateService).use('pl');

    fixture = TestBed.createComponent(Rules);
    http = TestBed.inject(HttpTestingController);
    internals = fixture.componentInstance as unknown as RulesInternals;
  });

  it('ustawia reguły w kolejności stosowania, czyli priorytetem rosnąco', async () => {
    // ⚠️ To nie jest preferencja układu. `RuleCategorizer` bierze PIERWSZE trafienie z listy
    // posortowanej priorytetem, więc niższa liczba wygrywa. Tabela w innej kolejności
    // ukrywałaby jedyną własność, która decyduje o wyniku kategoryzacji.
    await settle([
      rule({ priority: 90, categoryName: 'Gotówka' }),
      rule({ priority: 5, categoryName: 'Oszczędności' }),
      rule({ priority: 40, categoryName: 'Paliwo' }),
    ]);

    const priorities = [...fixture.nativeElement.querySelectorAll('tbody tr')]
      .map((row) => (row as HTMLElement).querySelector('td')!.textContent!.trim());

    expect(priorities).toEqual(['5', '40', '90']);
  });

  it('ostrzega o remisie priorytetów i pokazuje, których dotyczy', async () => {
    await settle([rule({ priority: 40 }), rule({ priority: 40 }), rule({ priority: 90 })]);

    expect(text()).toContain('Reguły o tym samym priorytecie: 40');
    expect(text()).toContain('Rozumiem');
  });

  it('nie ostrzega, gdy priorytety są różne', async () => {
    await settle([rule({ priority: 10 }), rule({ priority: 20 })]);

    expect(text()).not.toContain('tym samym priorytecie');
  });

  it('„Rozumiem" chowa ostrzeżenie i nie wraca przy kolejnej wizycie z tymi samymi remisami', async () => {
    // ⚠️ Na realnych danych remis jest normą (BaselineSeed grupuje reguły po priorytecie),
    // więc alert przy każdej wizycie byłby szumem. Pamiętany jest ZESTAW remisów.
    await settle([rule({ priority: 40 }), rule({ priority: 40 })]);
    clickButton('Rozumiem');
    expect(text()).not.toContain('tym samym priorytecie');

    await recreate([rule({ priority: 40 }), rule({ priority: 40 })]);

    expect(text()).not.toContain('tym samym priorytecie');
  });

  it('ostrzeżenie wraca, gdy pojawi się NOWY remis', async () => {
    // Zamknięcie na zawsze przestałoby ostrzegać o kolizji dopisanej później — a o to chodzi.
    await settle([rule({ priority: 40 }), rule({ priority: 40 })]);
    clickButton('Rozumiem');

    await recreate([rule({ priority: 40 }), rule({ priority: 40 }), rule({ priority: 90 }), rule({ priority: 90 })]);

    expect(text()).toContain('Reguły o tym samym priorytecie: 40, 90');
  });

  it('blokuje zapis reguły bez żadnego wzorca', async () => {
    // Taka reguła zapisałaby się i pasowała do KAŻDEJ transakcji po swojej stronie przepływu,
    // więc backend ją odrzuca — mówimy to przed wysłaniem, zamiast czekać na 400.
    await settle([]);
    internals.openCreate();
    internals.patch('categoryId', 'cat-1');
    fixture.detectChanges();

    expect(internals.draftError()).toBe('settings.rules.editor.patternRequired');
  });

  it('blokuje zapis, gdy widełki kwot nie mogą nic złapać', async () => {
    // Silnik sprawdza `abs < min` i `abs >= max`, więc min == max odrzuca wszystko.
    await settle([]);
    internals.openCreate();
    internals.patch('categoryId', 'cat-1');
    internals.patch('pattern', 'ABONAMENT');
    internals.patch('minAmount', 100);
    internals.patch('maxAmount', 100);
    fixture.detectChanges();

    expect(internals.draftError()).toBe('settings.rules.editor.rangeInvalid');
  });

  it('wymaga kategorii, bo reguła bez niej nie ma co przypisać', async () => {
    await settle([]);
    internals.openCreate();
    internals.patch('pattern', 'ABONAMENT');
    fixture.detectChanges();

    expect(internals.draftError()).toBe('settings.rules.editor.categoryRequired');
  });

  it('mówi, ile trafień zabiera reguła o wcześniejszym priorytecie', async () => {
    // ⚠️ Bez tej liczby podgląd obiecywałby skutek, którego reguła nie ma: łapie 2, ale
    // jedną zabiera reguła stojąca wyżej, więc skategoryzuje tylko jedną.
    await settle([]);
    internals.openCreate();
    internals.patch('categoryId', 'cat-1');
    internals.patch('pattern', 'STACJA PALIW');
    fixture.detectChanges();

    const pending = internals.runPreview();
    const request = http.expectOne('/api/categorization/rules/preview');
    request.flush({
      matchCount: 2,
      shadowedCount: 1,
      scannedCount: 120,
      samples: [{
        id: 'tx-1', date: '2026-08-11', description: 'STACJA PALIW A4',
        transactionType: 'Obciążenie', amount: -301.15, currentCategoryName: null,
      }],
    } satisfies RulePreview);
    await pending;
    fixture.detectChanges();

    expect(internals.preview()?.shadowedCount).toBe(1);
    expect(modalText()).toContain('Pasuje 2 z 120 transakcji.');
    expect(modalText()).toContain('zabiera reguła o wcześniejszym priorytecie');
  });

  it('przy zerze trafień mówi, na ilu transakcjach liczono', async () => {
    // „0 trafień" bez tej liczby nie mówi, czy wzorzec jest zły, czy po prostu nie ma danych.
    await settle([]);
    internals.openCreate();
    internals.patch('categoryId', 'cat-1');
    internals.patch('pattern', 'NIE ISTNIEJE');
    fixture.detectChanges();

    const pending = internals.runPreview();
    http.expectOne('/api/categorization/rules/preview').flush({
      matchCount: 0, shadowedCount: 0, scannedCount: 120, samples: [],
    } satisfies RulePreview);
    await pending;
    fixture.detectChanges();

    expect(modalText()).toContain('Żadna z 120 transakcji nie pasuje.');
  });

  it('przed usunięciem mówi, czego usunięcie NIE robi', async () => {
    // ⚠️ Naturalne założenie jest odwrotne — że skasowanie reguły cofnie jej skutki.
    // Nie cofa: transakcje zostają w swoich kategoriach, a tego nie da się odkliknąć.
    const target = rule({ priority: 40, categoryName: 'Paliwo', pattern: 'STACJA PALIW' });
    await settle([target]);

    const pending = internals.remove(target);
    await Promise.resolve();

    expect(confirmDialog.lastOptions?.header).toContain('STACJA PALIW');
    expect(confirmDialog.lastOptions?.header).toContain('priorytet 40');
    expect(confirmDialog.lastOptions?.description).toContain('zostają bez zmian');

    confirmDialog.respond(false);
    await pending;

    http.expectNone((r) => r.method === 'DELETE');
  });

  it('usuwa dopiero po potwierdzeniu', async () => {
    const target = rule();
    await settle([target]);

    const pending = internals.remove(target);
    await Promise.resolve();
    confirmDialog.respond(true);

    // ⚠️ Żądanie DELETE trzeba wypuścić PRZED `await pending`, inaczej test się zakleszcza:
    // `remove()` czeka na odpowiedź, której nikt jeszcze nie podał.
    await new Promise((resolve) => setTimeout(resolve, 0));
    http.expectOne((r) => r.method === 'DELETE' && r.url === `/api/categorization/rules/${target.id}`)
      .flush(null);

    await pending;
  });
});
