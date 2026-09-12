import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { provideNzDateFnsAdapter } from 'ng-zorro-antd/core/time';
import { APP_ICONS } from '../../core/icons';
import { Import } from './import';
import { ImportSummary } from '../../core/api/models/import-summary';
import { EditableRow } from './editable-row';

/**
 * Krok 3 i 4 steppera importu — te fragmenty, w których makieta i moja pierwsza wersja
 * się rozjechały: status jako kropka (nz-badge, nie nz-tag), filtry na nagłówkach
 * zamiast sortowania, oraz podsumowanie na nz-statistic.
 */
describe('Import', () => {
  let fixture: ComponentFixture<Import>;
  let component: Import;

  const api = () => component as unknown as {
    step: { set(v: number): void };
    rows: { set(v: EditableRow[]): void; (): EditableRow[] };
    summary: { set(v: ImportSummary): void };
    menuRow: { set(v: EditableRow): void };
    draftName: { set(v: string): void };
    draftAmount: { set(v: number): void };
    draftDate: { set(v: Date): void };
    openEdit(): void;
    saveEdit(): void;
    removeRow(): void;
    updateCategory(row: EditableRow, categoryId: string | null): void;
    categoryFilters(): { text: string; value: string }[];
    filterByStatus(selected: string[], row: EditableRow): boolean;
    sortByCategory(a: EditableRow, b: EditableRow): number;
    sortByAmount(a: EditableRow, b: EditableRow): number;
    sortByStatus(a: EditableRow, b: EditableRow): number;
    sortByConfidence(a: EditableRow, b: EditableRow): number;
    canAccept(): boolean;
    willImport(): number;
    selected: { set(v: ReadonlySet<number>): void };
    acceptableCount(): number;
    acceptSelected(): void;
    pendingReview(): number;
    acceptSuggestion(): void;
  };

  const row = (over: Partial<EditableRow>): EditableRow => ({
    index: 0, date: '2026-08-31', amount: -123.45, description: 'SKLEP',
    transactionType: 'Obciążenie', externalReference: 'R1',
    categoryId: 'c1000000-0000-4000-8000-000000000001', categoryName: 'Jedzenie', confidence: 0.9,
    duplicate: false, needsReview: false, edited: false, ...over,
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Import],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'dashboard', children: [] }]),
        provideNoopAnimations(),
        provideTranslateService(),
        provideNzIcons(APP_ICONS),
        // Te same dwa co w `app.config.ts`: `nz-date-picker` w modalu edycji wymaga
        // adaptera dat, a bez lokalizacji NG-ZORRO nie wyrenderuje własnych tekstów.
        provideNzI18n(pl_PL),
        provideNzDateFnsAdapter(),
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('pl', {
      import: {
        preview: {
          noCategory: 'Bez kategorii',
          edit: 'Edytuj',
          remove: 'Usuń',
          editTitle: 'Edycja transakcji',
          editName: 'Nazwa transakcji',
          editDate: 'Data transakcji',
          status: { ok: 'Poprawne', review: 'Do weryfikacji', duplicate: 'Duplikat' },
          accept: 'Akceptuj podpowiedź',
          acceptSelected: 'Akceptuj zaznaczone ({{count}})',
          acceptConfirm: 'Przyjąć podpowiedzi dla {{count}} wierszy?',
          removeSelected: 'Usuń zaznaczone',
          removeConfirm: 'Usunąć zaznaczone wiersze ({{count}})?',
          columns: { category: 'Kategoria', amount: 'Kwota', actions: 'Akcje' },
        },
      },
    });
    translate.use('pl');

    fixture = TestBed.createComponent(Import);
    component = fixture.componentInstance;
    fixture.detectChanges();
    TestBed.inject(HttpTestingController)
      .match('/api/import/sources')
      .forEach((r) => r.flush({
        banks: ['pko'],
        budgets: [],
        categories: [
          { id: 'c1000000-0000-4000-8000-000000000001', name: 'Jedzenie' },
          { id: 'c2000000-0000-4000-8000-000000000002', name: 'Paliwo' },
        ],
      }));
    await fixture.whenStable();
  });

  // ── Krok 4: podsumowanie ───────────────────────────────────────────────────────────

  it('pokazuje pełny zakres dat, a nie samą pierwszą datę', async () => {
    // Regresja: `nz-statistic` z `nzValue` traktuje wartość jak liczbę i formatuje ją
    // po swojemu — zakres „31.08.2026 – 30.09.2026" ucinał do „31.08".
    api().summary.set(summary({ periodFrom: '2026-08-31', periodTo: '2026-09-30' }));
    api().step.set(3);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('31.08.2026');
    expect(text).toContain('30.09.2026');
  });

  it('grupuje tysiące także w kwotach czterocyfrowych', async () => {
    // `Intl` z domyślnym `useGrouping: 'auto'` NIE grupuje w `pl` liczb czterocyfrowych,
    // więc obok siebie wychodziło „8857,66" i „12 345,60". Makieta grupuje zawsze.
    api().summary.set(summary({ budgetBalance: 8857.66 }));
    api().step.set(3);
    fixture.detectChanges();

    // `Intl` w pl-PL rozdziela tysiace spacja NIEROZDZIELAJACA (waska albo zwykla),
    // wiec porownujemy po znormalizowaniu wszystkich odmian bialych znakow.
    const text = (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');
    expect(text).toContain('8 857,66 PLN');
  });

  // ── Krok 3: filtry ─────────────────────────────────────────────────────────────────

  it('buduje filtr kategorii z wierszy, które są w tabeli', () => {
    api().rows.set([
      row({ index: 0, categoryName: 'Jedzenie' }),
      row({ index: 1, categoryName: 'Paliwo' }),
      row({ index: 2, categoryName: 'Jedzenie' }),
    ]);

    // Bez duplikatów i bez kategorii, których w tabeli nie ma.
    expect(api().categoryFilters().map((f) => f.text)).toEqual(['Jedzenie', 'Paliwo']);
  });

  // ── Krok 3: sortowanie ─────────────────────────────────────────────────────────────

  it('sortuje kategorię alfabetycznie, z wierszami „bez kategorii" na końcu', () => {
    const bezKategorii = row({ index: 0, categoryId: null, categoryName: null });
    const jedzenie = row({ index: 1, categoryName: 'Jedzenie' });
    const paliwo = row({ index: 2, categoryName: 'Paliwo' });

    // Rosnąco: alfabetycznie by „Bez kategorii" nie miało gdzie trafić — te wiersze
    // wymagają uwagi, więc nie mają się wpychać przed cokolwiek nazwanego.
    expect(api().sortByCategory(jedzenie, paliwo)).toBeLessThan(0);
    expect(api().sortByCategory(jedzenie, bezKategorii)).toBeLessThan(0);
    expect(api().sortByCategory(bezKategorii, bezKategorii)).toBe(0);
  });

  it('sortuje kwotę numerycznie, nie tekstowo', () => {
    // Regresja: porównanie tekstowe dałoby "9" > "80" (leksykograficznie '9' > '8'),
    // choć liczbowo 9 < 80 — kwota musi się sortować jako liczba.
    expect(api().sortByAmount(row({ amount: 9 }), row({ amount: 80 }))).toBeLessThan(0);
  });

  it('sortuje status w kolejności logicznej (poprawne → do weryfikacji → duplikat)', () => {
    const ok = row({ categoryId: 'c1', duplicate: false });
    // Wiersz do weryfikacji MA teraz kategorię — o statusie decyduje `needsReview`, nie jej brak.
    const review = row({ needsReview: true, duplicate: false });
    const duplicate = row({ duplicate: true });

    expect(api().sortByStatus(ok, review)).toBeLessThan(0);
    expect(api().sortByStatus(review, duplicate)).toBeLessThan(0);
  });

  it('sortuje trafność, z brakiem predykcji jako NAJNIŻSZĄ wartością, nie zerem', () => {
    const brak = row({ confidence: null });
    const zero = row({ confidence: 0 });
    const wysoka = row({ confidence: 0.9 });

    expect(api().sortByConfidence(brak, zero)).toBeLessThan(0);
    expect(api().sortByConfidence(zero, wysoka)).toBeLessThan(0);
  });

  it('dokłada pozycję „Bez kategorii", gdy są wiersze do weryfikacji', () => {
    api().rows.set([
      row({ index: 0, categoryName: 'Jedzenie' }),
      row({ index: 1, categoryId: null, categoryName: null }),
    ]);

    expect(api().categoryFilters().map((f) => f.text)).toContain('Bez kategorii');
  });

  // ── Menu wiersza i modal edycji ────────────────────────────────────────────────────

  it('zapisuje zmiany z modala do wiersza', () => {
    api().rows.set([row({ index: 0, description: 'ORANGE POLSKA S.A.', amount: -100 })]);
    api().menuRow.set(api().rows()[0]);
    api().openEdit();

    api().draftName.set('  Orange  ');
    api().draftAmount.set(-128.43);
    api().draftDate.set(new Date(2025, 2, 31));   // 31.03.2025, czas lokalny
    api().saveEdit();

    const updated = api().rows()[0];
    expect(updated.description).toBe('Orange');
    expect(updated.amount).toBe(-128.43);
    // Data z pickera to lokalny Date — zapis MUSI zostać przy 31.03, nie cofnąć się
    // o dzień przez przeliczenie na UTC.
    expect(updated.date).toBe('2025-03-31');
  });

  it('oznacza wiersz jako poprawiony tylko wtedy, gdy zmieniła się kategoria', () => {
    // `edited` decyduje o statusie po imporcie (ManuallyCategorized). Poprawka nazwy
    // czy kwoty nie jest decyzją o kategorii, więc nie może udawać korekty.
    api().rows.set([row({ index: 0, categoryName: 'Jedzenie' })]);
    api().menuRow.set(api().rows()[0]);
    api().openEdit();
    api().draftName.set('Inna nazwa');
    api().saveEdit();

    expect(api().rows()[0].edited).toBe(false);
  });

  // ── Kategoria wprost w komórce ─────────────────────────────────────────────────────

  it('poprawia kategorię bez modala i wyprowadza nazwę z tego samego słownika co wszędzie', () => {
    const target = row({
      index: 0,
      categoryId: 'c1000000-0000-4000-8000-000000000001',
      categoryName: 'Jedzenie',
    });
    api().rows.set([target]);

    api().updateCategory(target, 'c2000000-0000-4000-8000-000000000002');

    const updated = api().rows()[0];
    expect(updated.categoryId).toBe('c2000000-0000-4000-8000-000000000002');
    expect(updated.categoryName).toBe('Paliwo');
    expect(updated.edited).toBe(true);
  });

  it('nie oznacza wiersza jako poprawionego, gdy wybrano tę samą kategorię', () => {
    const target = row({
      index: 0,
      categoryId: 'c1000000-0000-4000-8000-000000000001',
      categoryName: 'Jedzenie',
      edited: false,
    });
    api().rows.set([target]);

    api().updateCategory(target, 'c1000000-0000-4000-8000-000000000001');

    expect(api().rows()[0].edited).toBe(false);
  });

  it('czyści kategorię do „brak", gdy wybrano opcję pustą (nzAllowClear)', () => {
    const target = row({
      index: 0,
      categoryId: 'c1000000-0000-4000-8000-000000000001',
      categoryName: 'Jedzenie',
    });
    api().rows.set([target]);

    api().updateCategory(target, null);

    const updated = api().rows()[0];
    expect(updated.categoryId).toBeNull();
    expect(updated.categoryName).toBeNull();
    expect(updated.edited).toBe(true);
  });

  it('daje select w komórce „Kategoria" każdemu wierszowi, ale nie duplikatom', async () => {
    // Duplikat i tak nie zostanie zapisany — jego kategorię pokazujemy jako tekst,
    // tak jak resztę jego danych, a nie jako pole do edycji.
    api().rows.set([row({ index: 0 }), row({ index: 1, duplicate: true })]);
    api().step.set(2);
    fixture.detectChanges();

    // Liczymy same SELECTY, nie wiersze — z tego samego powodu co przy przycisku akcji:
    // nz-table dokłada do `tbody` ukryty wiersz pomiarowy.
    const selects = fixture.nativeElement.querySelectorAll('tbody .imp__category-cell');
    expect(selects.length).toBe(1);
  });

  it('daje każdemu wierszowi przycisk akcji, ale nie duplikatom', async () => {
    // Samego rozwijania menu nie testujemy: overlay otwiera NG-ZORRO, nie nasz kod,
    // a programowy `click()` go nie wyzwala. To, co nasze — że przycisk jest tam,
    // gdzie ma być, i że wywołuje właściwą akcję — pokrywają testy openEdit/removeRow.
    api().rows.set([row({ index: 0 }), row({ index: 1, duplicate: true })]);
    api().step.set(2);
    fixture.detectChanges();

    // Liczymy same PRZYCISKI, nie wiersze: nz-table dokłada do `tbody` ukryty wiersz
    // pomiarowy z kompletem komórek, przez co liczenie wierszy myli.
    const triggers = fixture.nativeElement.querySelectorAll('tbody td:last-child button');

    // Dwa wiersze w tabeli, ale duplikat nie dostaje przycisku — i tak nie zostanie
    // zapisany, więc nie ma czego edytować ani usuwać.
    expect(triggers.length).toBe(1);
  });

  it('wyświetla pola modala edycji wypełnione danymi wiersza', async () => {
    api().rows.set([row({ index: 0, description: 'ORANGE POLSKA S.A.' })]);
    api().step.set(2);
    fixture.detectChanges();

    api().menuRow.set(api().rows()[0]);
    api().openEdit();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const nameInput = document.body.querySelector('#edit-name') as HTMLInputElement | null;
    // Brak tego pola = modal edycji sie nie wyrenderowal.
    expect(nameInput).toBeTruthy();
    expect(nameInput!.value).toBe('ORANGE POLSKA S.A.');
    expect(document.body.querySelector('#edit-amount')).toBeTruthy();
    expect(document.body.querySelector('#edit-date')).toBeTruthy();
  });

  it('usuwa pojedynczy wiersz z menu', () => {
    api().rows.set([row({ index: 0 }), row({ index: 1, description: 'DRUGI' })]);
    api().menuRow.set(api().rows()[0]);
    api().removeRow();

    expect(api().rows().map((r) => r.description)).toEqual(['DRUGI']);
  });

  it('filtr statusu rozróżnia poprawne, do weryfikacji i duplikaty', () => {
    const ok = row({ index: 0 });
    const review = row({ index: 1, needsReview: true });
    const duplicate = row({ index: 2, duplicate: true });

    expect(api().filterByStatus(['ok'], ok)).toBe(true);
    expect(api().filterByStatus(['ok'], review)).toBe(false);
    expect(api().filterByStatus(['review'], review)).toBe(true);
    expect(api().filterByStatus(['duplicate'], duplicate)).toBe(true);
    // Duplikat z kategoria nie moze uchodzic za „poprawny" — decyduje bycie duplikatem.
    expect(api().filterByStatus(['ok'], duplicate)).toBe(false);
    // Pusty wybor = filtr wylaczony, przepuszcza wszystko.
    expect(api().filterByStatus([], duplicate)).toBe(true);
  });

  // ── Akceptacja podpowiedzi ───────────────────────────────────────────────────────────

  it('podpowiedź poniżej progu zostaje w wierszu, ale wiersz dalej czeka na weryfikację', () => {
    // Sedno zmiany: zamiast pustego pola i wyboru z 25 kategorii użytkownik dostaje
    // propozycję, którą wystarczy potwierdzić albo poprawić. Status ma się przez to
    // NIE zmienić — inaczej niepewne trafienie modelu uchodziłoby za sprawdzone.
    const suggested = row({
      categoryId: 'c1', categoryName: 'Gastronomia', confidence: 0.46, needsReview: true,
    });

    expect(api().filterByStatus(['review'], suggested)).toBe(true);
    expect(api().filterByStatus(['ok'], suggested)).toBe(false);
  });

  it('„Akceptuj" jest dostępne tylko tam, gdzie jest co akceptować', () => {
    api().menuRow.set(row({ needsReview: true, categoryId: 'c1' }));
    expect(api().canAccept()).toBe(true);

    // Nic nie podpowiedziano — nie ma czego przyjąć, trzeba wybrać.
    api().menuRow.set(row({ needsReview: true, categoryId: null, categoryName: null }));
    expect(api().canAccept()).toBe(false);

    // Duplikat i tak nie idzie do zapisu.
    api().menuRow.set(row({ needsReview: true, categoryId: 'c1', duplicate: true }));
    expect(api().canAccept()).toBe(false);

    // Nic nie czeka na decyzję.
    api().menuRow.set(row({ needsReview: false, categoryId: 'c1' }));
    expect(api().canAccept()).toBe(false);
  });

  it('akceptacja zdejmuje wiersz z kolejki i czyni go decyzją CZŁOWIEKA', () => {
    // `edited` nie jest kosmetyką: rozstrzyga status po zapisie, a tylko decyzje człowieka
    // wracają potem do zbioru treningowego. Akceptacja bez tego nie douczyłaby modelu niczego.
    const suggested = row({ index: 0, categoryId: 'c1', confidence: 0.46, needsReview: true });
    api().rows.set([suggested]);
    api().menuRow.set(suggested);

    api().acceptSuggestion();

    const saved = api().rows()[0];
    expect(saved.needsReview).toBe(false);
    expect(saved.edited).toBe(true);
    expect(saved.categoryId).toBe('c1');
  });

  it('poprawienie kategorii też zdejmuje wiersz z kolejki', () => {
    const suggested = row({ index: 0, categoryId: 'c1', confidence: 0.46, needsReview: true });
    api().rows.set([suggested]);

    api().updateCategory(suggested, 'c2');

    expect(api().rows()[0].needsReview).toBe(false);
    expect(api().rows()[0].edited).toBe(true);
  });

  it('wyczyszczenie kategorii ZOSTAWIA wiersz w kolejce', () => {
    // Bez kategorii wiersz i tak zapisze się jako „do przeglądu" — pokazanie go jako
    // poprawnego byłoby obietnicą, której zapis nie dotrzyma.
    const suggested = row({ index: 0, categoryId: 'c1', confidence: 0.46, needsReview: true });
    api().rows.set([suggested]);

    api().updateCategory(suggested, null);

    expect(api().rows()[0].needsReview).toBe(true);
  });

  it('PLAKIETKA w tabeli mówi to samo, co filtr statusu', async () => {
    // ⚠️ REGRESJA. Szablon miał własną kopię reguły („ma kategorię = poprawne") i po zmianie
    // reguły w kodzie został przy starej: wiersz z pewnością 31% świecił „Poprawne", choć
    // filtr klasyfikował go jako „do weryfikacji". Poprzednie testy tego nie łapały, bo
    // pytały o `filterByStatus`, a nie o to, co widać na ekranie — dlatego ten czyta DOM.
    api().step.set(2);
    api().rows.set([
      row({ index: 0, categoryId: 'c1', confidence: 0.31, needsReview: true }),
      row({ index: 1, categoryId: 'c1', confidence: 0.95, needsReview: false }),
    ]);
    fixture.detectChanges();

    const badges = Array.from(
      fixture.nativeElement.querySelectorAll('tbody tr nz-badge') as NodeListOf<HTMLElement>,
    ).map((b) => (b.textContent ?? '').trim());

    // Teksty przechodzą przez tłumaczenia, więc sprawdzamy to, co NAPRAWDĘ widzi użytkownik.
    expect(badges[0]).toBe('Do weryfikacji');
    expect(badges[1]).toBe('Poprawne');
  });

  it('licznik „do weryfikacji" zgadza się ze statusami w tabeli', () => {
    // ⚠️ REGRESJA. Licznik miał własną kopię reguły („ma kategorię = do zapisania") i przy
    // 1335 wierszach pokazywał „do weryfikacji: 2" — tyle było wierszy ZUPEŁNIE bez kategorii,
    // choć setki czekały na przegląd z podpowiedzią. Podsumowanie nad tabelą mówiło wtedy
    // co innego niż plakietki w samej tabeli.
    api().rows.set([
      row({ index: 0, categoryId: 'c1', confidence: 0.31, needsReview: true }),   // podpowiedź
      row({ index: 1, categoryId: null, categoryName: null, needsReview: true }), // brak zdania
      row({ index: 2, categoryId: 'c1', confidence: 0.95, needsReview: false }),  // pewne
      row({ index: 3, categoryId: 'c1', duplicate: true }),                       // nie liczy się
    ]);

    expect(api().pendingReview()).toBe(2);
    expect(api().willImport()).toBe(1);
  });

  it('licznik przy „Akceptuj zaznaczone" liczy tylko to, na co akcja zadziała', () => {
    // Zaznaczenie robi się filtrem i myszą, więc obejmuje też wiersze poprawne albo takie,
    // dla których nie ma czego przyjąć. Przycisk obiecujący „4", a zmieniający 1, byłby
    // gorszy niż brak licznika.
    api().rows.set([
      row({ index: 0, categoryId: 'c1', confidence: 0.31, needsReview: true }),   // ✓
      row({ index: 1, categoryId: null, categoryName: null, needsReview: true }), // brak zdania
      row({ index: 2, categoryId: 'c1', confidence: 0.95, needsReview: false }),  // już poprawny
      row({ index: 3, categoryId: 'c1', needsReview: true, duplicate: true }),    // nie zapisujemy
    ]);
    api().selected.set(new Set([0, 1, 2, 3]));

    expect(api().acceptableCount()).toBe(1);
  });

  it('hurtowa akceptacja przyjmuje kwalifikujące się i nie tyka reszty', () => {
    const rows = [
      row({ index: 0, categoryId: 'c1', confidence: 0.31, needsReview: true }),
      row({ index: 1, categoryId: 'c2', confidence: 0.44, needsReview: true }),
      row({ index: 2, categoryId: null, categoryName: null, needsReview: true }),
      row({ index: 3, categoryId: 'c1', confidence: 0.95, needsReview: false }),
    ];
    api().rows.set(rows);
    api().selected.set(new Set([0, 1, 2, 3]));

    api().acceptSelected();

    const after = api().rows();
    // Przyjęte: kategoria bez zmian, ale autorem jest teraz człowiek.
    expect(after[0]).toMatchObject({ needsReview: false, edited: true, categoryId: 'c1' });
    expect(after[1]).toMatchObject({ needsReview: false, edited: true, categoryId: 'c2' });
    // Bez podpowiedzi nie ma czego przyjąć — zostaje w kolejce i NIE udaje decyzji człowieka.
    expect(after[2]).toMatchObject({ needsReview: true, edited: false });
    // Poprawny wiersz nie staje się przez to „ręczny".
    expect(after[3]).toMatchObject({ needsReview: false, edited: false });
  });

  it('hurtowa akceptacja pomija wiersze spoza zaznaczenia', () => {
    api().rows.set([
      row({ index: 0, categoryId: 'c1', confidence: 0.31, needsReview: true }),
      row({ index: 1, categoryId: 'c1', confidence: 0.31, needsReview: true }),
    ]);
    api().selected.set(new Set([0]));

    api().acceptSelected();

    expect(api().rows()[0].needsReview).toBe(false);
    expect(api().rows()[1].needsReview).toBe(true);
  });

  it('PRZYCISK „Akceptuj zaznaczone" pokazuje realną liczbę i gaśnie, gdy nie ma czego przyjąć', () => {
    // Testy logiki nie wystarczają — dwa razy z rzędu rozjechał się właśnie WIDOK, bo miał
    // własną kopię reguły. Ten test czyta wyrenderowany przycisk.
    api().step.set(2);
    api().rows.set([
      row({ index: 0, categoryId: 'c1', confidence: 0.31, needsReview: true }),
      row({ index: 1, categoryId: 'c1', confidence: 0.95, needsReview: false }),
    ]);

    // Zaznaczony tylko wiersz, którego nie ma czego akceptować → przycisk wygaszony.
    api().selected.set(new Set([1]));
    fixture.detectChanges();

    const button = (): HTMLButtonElement | null => Array
      .from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find((b) => (b.textContent ?? '').includes('Akceptuj zaznaczone')) ?? null;

    expect(button()).not.toBeNull();
    expect(button()!.disabled).toBe(true);
    expect(button()!.textContent).toContain('(0)');

    // Dochodzi wiersz z podpowiedzią poniżej progu → przycisk ożywa i liczy JEGO.
    api().selected.set(new Set([0, 1]));
    fixture.detectChanges();

    expect(button()!.disabled).toBe(false);
    expect(button()!.textContent).toContain('(1)');
  });
});

function summary(over: Partial<ImportSummary> = {}): ImportSummary {
  return {
    batchId: 'b1000000-0000-4000-8000-000000000001', rowsInFile: 1, imported: 1, pendingReview: 0, skippedDuplicates: 0,
    totalExpenses: 123.45, totalIncome: 0, periodFrom: '2026-08-31', periodTo: '2026-08-31',
    averageConfidence: 0.9, budgetBalance: 1000, ...over,
  };
}
