import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDropdownModule } from 'ng-zorro-antd/dropdown';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzStepsModule } from 'ng-zorro-antd/steps';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ImportSources } from '../../core/api/models/import-sources';
import { ImportSummary } from '../../core/api/models/import-summary';
import { PreviewRow } from '../../core/api/models/preview-row';
import { ImportPreview } from '../../core/api/models/import-preview';
import { EditableRow } from './editable-row';
import { PageHeader } from '../../core/page-header/page-header';
import { ErrorMessages } from '../../core/errors/error-messages';
import { valueOf } from '../../core/api/resource-value';

/** Wartość filtra dla wierszy bez kategorii — pusty string myliłby się z „brak wyboru". */
const NoCategory = '__brak__';

/**
 * `Date` → `YYYY-MM-DD` w czasie LOKALNYM. `toISOString()` przelicza na UTC, więc
 * data wybrana wieczorem potrafiłaby cofnąć się o dzień.
 */
function toIsoDay(date: Date | null): string | null {
  if (!date) return null;
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/**
 * Trzy stany wiersza podglądu, w tej samej kolejności co znaczniki w tabeli.
 *
 * ⚠️ Rozstrzyga `needsReview` z serwera, a NIE obecność kategorii. Odkąd niepewna podpowiedź
 * też trafia do wiersza (żeby wystarczyło ją potwierdzić zamiast wybierać z 25 kategorii),
 * „ma kategorię" przestało znaczyć „gotowe" — po staremu wszystkie te wiersze udawałyby
 * poprawne. Progu nie liczymy u siebie: znałby go wtedy i front, i backend, i rozjechałyby
 * się przy pierwszej zmianie.
 */
function statusOf(row: { duplicate: boolean; needsReview: boolean }): 'ok' | 'review' | 'duplicate' {
  if (row.duplicate) return 'duplicate';
  return row.needsReview ? 'review' : 'ok';
}

/**
 * Kroki wg makiety: Konto → Plik CSV → Podgląd → Gotowe.
 *
 * Ramki kroku 1 w Figmie pokazują jeszcze piąty krok „Konfiguracja konwertera", ale
 * nowsze ramki (kroki 2–4) już go nie mają, a CLAUDE.md §6 wprost odrzuca mapowanie
 * kolumn w UI — parser per bank zna format z góry. Decyzja użytkownika: 4 kroki.
 */
enum Step {
  Source = 0,
  File = 1,
  Preview = 2,
  Done = 3,
}

@Component({
  selector: 'app-import',
  imports: [
    CommonModule, FormsModule, RouterLink,
    NzAlertModule, NzBadgeModule, NzButtonModule, NzDatePickerModule, NzDropdownModule,
    NzEmptyModule, NzIconModule, NzInputModule, NzInputNumberModule, NzModalModule,
    NzPopconfirmModule, NzSelectModule, NzSpinModule, NzStatisticModule, NzStepsModule, NzTableModule,
    NzUploadModule, TranslatePipe, PageHeader,
  ],
  templateUrl: './import.html',
  styleUrl: './import.scss',
})
export class Import {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly message = inject(NzMessageService);
  private readonly errorMessages = inject(ErrorMessages);

  protected readonly Step = Step;
  protected readonly step = signal<Step>(Step.Source);

  /**
   * Komunikat błędu przychodzi GOTOWY z API — backend trzyma teksty w `.resx` i tłumaczy
   * je wg kultury żądania. Front dokłada tylko własny komunikat na awarię sieci.
   */
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  // ── Krok 1: konto ────────────────────────────────────────────────────────────────────

  /** Banki, budżety i kategorie jednym żądaniem — stepper i tak potrzebuje kompletu. */
  private readonly sourcesResource = httpResource<ImportSources>(() => '/api/import/sources');

  /** Bezpieczny odczyt — surowe `value` RZUCA w stanie błędu (patrz core/api/resource-value.ts). */
  protected readonly sources = valueOf(this.sourcesResource);
  protected readonly sourcesLoading = this.sourcesResource.isLoading;

  protected readonly bank = signal<string | null>(null);
  protected readonly budgetId = signal<string | null>(null);

  /**
   * Makieta ma osobny stan „Brak budżetu w aplikacji" z odnośnikiem „Utwórz budżet"
   * i wygaszonym „Dalej". Import bez budżetu nie ma się na czym zaksięgować.
   */
  protected readonly noBudgets = computed(
    () => !this.sourcesLoading() && (this.sources()?.budgets.length ?? 0) === 0,
  );

  protected readonly canLeaveSource = computed(
    () => this.bank() !== null && this.budgetId() !== null,
  );

  // ── Krok 2: plik ─────────────────────────────────────────────────────────────────────

  protected readonly file = signal<File | null>(null);

  /**
   * Status kroku „Plik CSV" — czerwony, gdy plik odpadł na walidacji (CLAUDE.md §7).
   *
   * Pozostałe wartości musimy podać SAMI, bo jawny `nzStatus` wyłącza wyliczanie
   * z `nzCurrent`: podanie na stałe „process" zapalało ten krok jako bieżący nawet
   * wtedy, gdy użytkownik stał dopiero na wyborze konta.
   */
  protected readonly fileStepStatus = computed<'wait' | 'process' | 'finish' | 'error'>(() => {
    if (this.error() !== null && this.step() === Step.File) return 'error';
    if (this.step() > Step.File) return 'finish';
    if (this.step() === Step.File) return 'process';
    return 'wait';
  });

  // ── Krok 3: podgląd ──────────────────────────────────────────────────────────────────

  /**
   * Robocza kopia wierszy z podglądu — jedyne źródło prawdy dla kroku 3.
   *
   * Użytkownik może wiersze usuwać i poprawiać im kategorie, więc liczniki nad tabelą
   * liczymy z TEGO stanu, a nie z odpowiedzi serwera: ta jest nieaktualna od pierwszej
   * edycji i pokazywałaby co innego, niż widać w tabeli.
   */
  protected readonly rows = signal<EditableRow[]>([]);

  protected readonly selected = signal<ReadonlySet<number>>(new Set());
  protected readonly search = signal('');

  /** Duplikatów nie zapisujemy, więc nie liczą się do niczego poza własnym licznikiem. */
  private readonly keptRows = computed(() => this.rows().filter((r) => !r.duplicate));

  /** Ile wierszy faktycznie pójdzie do zapisu — bez tego „Zapisz import" nie ma czego wyłączyć. */
  protected readonly keptRowsCount = computed(() => this.keptRows().length);

  protected readonly visibleRows = computed(() => {
    const needle = this.search().trim().toLowerCase();
    if (!needle) return this.rows();
    return this.rows().filter(
      (r) => r.description.toLowerCase().includes(needle)
        || (r.categoryName ?? '').toLowerCase().includes(needle),
    );
  });

  /**
   * ⚠️ Liczniki idą przez `statusOf`, tę samą funkcję co plakietka, filtr i sortowanie.
   *
   * Miały własną kopię reguły („ma kategorię = do zapisania") i po zmianie znaczenia progu
   * zostały przy starej: przy 1335 wierszach pokazywały „do weryfikacji: 2", bo tylko dwa
   * wiersze były ZUPEŁNIE bez kategorii — a kilkaset czekało na przegląd z podpowiedzią.
   * Podsumowanie nad tabelą mówiło więc co innego niż statusy w samej tabeli.
   */
  protected readonly willImport = computed(
    () => this.keptRows().filter((r) => statusOf(r) === 'ok').length,
  );

  protected readonly pendingReview = computed(
    () => this.keptRows().filter((r) => statusOf(r) === 'review').length,
  );

  protected readonly duplicates = computed(() => this.rows().filter((r) => r.duplicate).length);

  /**
   * Kolumny są, jak w reszcie apki, i FILTROWALNE, i SORTOWALNE.
   *
   * Listę kategorii budujemy z TEGO, co faktycznie jest w podglądzie, a nie ze słownika
   * wszystkich kategorii: filtr po wartości, której w tabeli nie ma, tylko myli.
   */
  protected readonly categoryFilters = computed(() => {
    const present = new Set(this.rows().map((r) => r.categoryName).filter((n): n is string => n !== null));
    const filters = [...present].sort((a, b) => a.localeCompare(b, 'pl')).map((n) => ({ text: n, value: n }));

    // Osobna pozycja na wiersze bez kategorii — to one wymagają uwagi użytkownika.
    if (this.rows().some((r) => r.categoryName === null)) {
      filters.push({ text: this.translate.instant('import.preview.noCategory'), value: NoCategory });
    }
    return filters;
  });

  protected readonly filterByCategory = (selected: string[], row: EditableRow): boolean =>
    selected.length === 0
    || selected.includes(row.categoryName ?? NoCategory);

  protected readonly statusFilters = computed(() => [
    { text: this.translate.instant('import.preview.status.ok'), value: 'ok' },
    { text: this.translate.instant('import.preview.status.review'), value: 'review' },
    { text: this.translate.instant('import.preview.status.duplicate'), value: 'duplicate' },
  ]);

  protected readonly filterByStatus = (selected: string[], row: EditableRow): boolean =>
    selected.length === 0 || selected.includes(statusOf(row));

  protected readonly sortByDate = (a: EditableRow, b: EditableRow): number =>
    a.date.localeCompare(b.date);

  // Wiersze bez kategorii (do przeglądu) na końcu — to one wymagają uwagi, więc
  // przy sortowaniu rosnącym nie mają wpychać się przed cokolwiek nazwanego.
  protected readonly sortByCategory = (a: EditableRow, b: EditableRow): number => {
    if (a.categoryName === null) return b.categoryName === null ? 0 : 1;
    if (b.categoryName === null) return -1;
    return a.categoryName.localeCompare(b.categoryName, 'pl');
  };

  protected readonly sortByName = (a: EditableRow, b: EditableRow): number =>
    a.description.localeCompare(b.description, 'pl');

  protected readonly sortByAmount = (a: EditableRow, b: EditableRow): number =>
    a.amount - b.amount;

  /** Ta sama kolejność co pozycje w `statusFilters` — nie alfabetyczna. */
  private static readonly STATUS_ORDER: Record<'ok' | 'review' | 'duplicate', number> =
    { ok: 0, review: 1, duplicate: 2 };

  protected readonly sortByStatus = (a: EditableRow, b: EditableRow): number =>
    Import.STATUS_ORDER[statusOf(a)] - Import.STATUS_ORDER[statusOf(b)];

  // `null` (bez predykcji — kategorię wskazał człowiek albo wiersz jest duplikatem)
  // sortuje się jako NAJNIŻSZA trafność, nie jako zero — to dwie różne rzeczy.
  protected readonly sortByConfidence = (a: EditableRow, b: EditableRow): number =>
    (a.confidence ?? -1) - (b.confidence ?? -1);

  protected readonly allVisibleSelected = computed(() => {
    const visible = this.visibleRows();
    return visible.length > 0 && visible.every((r) => this.selected().has(r.index));
  });

  // ── Krok 4: gotowe ───────────────────────────────────────────────────────────────────

  protected readonly summary = signal<ImportSummary | null>(null);

  // ── Przepływ ─────────────────────────────────────────────────────────────────────────

  /**
   * `nz-upload` bez adresu docelowego: przechwytujemy plik i zwracamy `false`,
   * żeby komponent nie próbował wysłać go sam. Wysyłką steruje stepper, bo plik
   * idzie najpierw na podgląd, a dopiero po akceptacji na zapis.
   */
  protected readonly captureFile = (file: NzUploadFile): boolean => {
    // NzUploadFile opakowuje natywny File w `originFileObj`; przy ręcznym sterowaniu
    // uploadem sam obiekt bywa już natywnym File.
    const native = (file as unknown as { originFileObj?: File }).originFileObj
      ?? (file as unknown as File);

    this.file.set(native);
    void this.loadPreview(native);
    return false;
  };

  /** Podgląd: parsowanie + kategoryzacja BEZ zapisu. */
  private async loadPreview(file: File): Promise<void> {
    this.busy.set(true);
    this.error.set(null);

    try {
      const body = new FormData();
      body.append('file', file, file.name);

      const result = await firstValueFrom(
        this.http.post<ImportPreview>('/api/import/preview', body, {
          // Budżet jest potrzebny już na podglądzie: duplikaty liczą się w obrębie
          // budżetu, więc bez niego podgląd pokazałby inny wynik niż zapis.
          params: { bank: this.bank()!, budgetId: this.budgetId()! },
        }),
      );

      this.rows.set(result.rows.map((r) => this.toEditable(r)));
      this.selected.set(new Set());
      this.search.set('');
      this.step.set(Step.Preview);
    } catch (e) {
      this.error.set(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  private toEditable(row: PreviewRow): EditableRow {
    return { ...row, edited: false };
  }

  /** Zatwierdzenie: dopiero to zapisuje cokolwiek w bazie. */
  protected async commit(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);

    try {
      const result = await firstValueFrom(
        this.http.post<ImportSummary>('/api/import', {
          budgetId: this.budgetId(),
          bank: this.bank(),
          fileName: this.file()?.name ?? '',
          // Duplikaty i wiersze usunięte przez użytkownika nie jadą — serwer zapisuje
          // dokładnie to, co zostało w tabeli.
          rows: this.keptRows().map((r) => ({
            date: r.date,
            amount: r.amount,
            description: r.description,
            transactionType: r.transactionType,
            externalReference: r.externalReference,
            categoryId: r.categoryId,
            confidence: r.confidence,
            edited: r.edited,
          })),
        }),
      );

      this.summary.set(result);
      this.step.set(Step.Done);
    } catch (e) {
      this.error.set(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  // ── Edycja tabeli ────────────────────────────────────────────────────────────────────

  protected toggleRow(index: number, checked: boolean): void {
    const next = new Set(this.selected());
    if (checked) next.add(index); else next.delete(index);
    this.selected.set(next);
  }

  protected toggleAllVisible(checked: boolean): void {
    const next = new Set(this.selected());
    for (const row of this.visibleRows()) {
      if (checked) next.add(row.index); else next.delete(row.index);
    }
    this.selected.set(next);
  }

  protected removeSelected(): void {
    const toRemove = this.selected();
    if (toRemove.size === 0) return;

    this.rows.update((rows) => rows.filter((r) => !toRemove.has(r.index)));
    this.selected.set(new Set());
  }

  // ── Menu wiersza i modal „Edycja transakcji" ─────────────────────────────────────────

  /** Wiersz, nad którego menu stoi kursor. Ustawiany przy otwarciu `⋮`. */
  protected readonly menuRow = signal<EditableRow | null>(null);

  /** Wiersz otwarty w modalu edycji; `null` = modal zamknięty. */
  protected readonly editRow = signal<EditableRow | null>(null);

  protected readonly draftName = signal('');
  protected readonly draftCategoryId = signal<string | null>(null);
  protected readonly draftAmount = signal(0);
  protected readonly draftDate = signal<Date | null>(null);

  /**
   * Status wiersza dla PLAKIETKI w tabeli — ta sama funkcja, z której korzystają filtr
   * i sortowanie.
   *
   * ⚠️ Szablon nie może liczyć tego u siebie. Miał własny warunek („ma kategorię = poprawne")
   * i po zmianie reguły został przy starej: wiersz z pewnością 31% pokazywał się jako
   * „Poprawne", mimo że filtr statusu klasyfikował go już jako „do weryfikacji". Dwie kopie
   * jednej reguły rozjeżdżają się po cichu — widać dopiero na ekranie.
   */
  protected rowStatus(row: EditableRow): 'ok' | 'review' | 'duplicate' {
    return statusOf(row);
  }

  /**
   * Czy da się przyjąć podpowiedź bez zmieniania czegokolwiek.
   *
   * Duplikat odpada, bo i tak nie idzie do zapisu. Wiersz bez kategorii też — model ani
   * reguła nic nie wskazały, więc nie ma czego akceptować, trzeba wybrać.
   */
  protected readonly canAccept = computed(() => {
    const row = this.menuRow();
    return row !== null && !row.duplicate && row.needsReview && row.categoryId !== null;
  });

  /**
   * Zaznaczone wiersze, na które „Akceptuj zaznaczone" faktycznie zadziała.
   *
   * Zaznaczenie prawie zawsze obejmuje też wiersze poprawne albo takie, dla których nie ma
   * czego akceptować — liczenie ich do etykiety obiecywałoby zmianę, która nie nastąpi.
   */
  private readonly acceptableRows = computed(() => {
    const selected = this.selected();
    return this.rows().filter(
      (r) => selected.has(r.index) && !r.duplicate && r.needsReview && r.categoryId !== null,
    );
  });

  protected readonly acceptableCount = computed(() => this.acceptableRows().length);

  /**
   * Hurtowe przyjęcie podpowiedzi. Przy wyciągu na 1300 wierszy przyjmowanie po jednym
   * z menu wiersza jest teoretyczne — sensowny przepływ to przefiltrować po statusie,
   * posortować po trafności malejąco i przyjąć górę listy jednym ruchem.
   *
   * Wiersze niekwalifikujące się są POMIJANE, nie odrzucane: zaznaczenie robi się filtrem
   * i myszą, więc trafia w nie też to, czego akcja nie dotyczy — i to jest normalne.
   */
  protected acceptSelected(): void {
    const accepted = new Set(this.acceptableRows().map((r) => r.index));
    if (accepted.size === 0) return;

    this.rows.update((rows) => rows.map((r) => (accepted.has(r.index)
      ? { ...r, edited: true, needsReview: false }
      : r)));
  }

  /**
   * Przyjęcie podpowiedzi modelu bez zmiany kategorii.
   *
   * Kategoria zostaje ta sama, ale zmienia się jej AUTOR: od tej chwili stoi za nią człowiek,
   * więc wiersz wychodzi z kolejki i zapisze się jako decyzja ręczna, nie jako predykcja.
   * To nie kosmetyka — tylko decyzje człowieka wracają potem do zbioru treningowego
   * (CLAUDE.md §3, `TrainingSetBuilder.HumanDecisions`), więc akceptacja realnie douczy model,
   * a pozostawienie wiersza w kolejce nie douczy niczego.
   */
  protected acceptSuggestion(): void {
    const row = this.menuRow();
    if (row === null || row.categoryId === null) return;

    this.rows.update((rows) => rows.map((r) => (r.index === row.index
      ? { ...r, edited: true, needsReview: false }
      : r)));

    this.menuRow.set(null);
  }

  protected openEdit(): void {
    const row = this.menuRow();
    if (!row) return;

    this.draftName.set(row.description);
    this.draftCategoryId.set(row.categoryId);
    this.draftAmount.set(row.amount);
    this.draftDate.set(new Date(row.date));
    this.editRow.set(row);
  }

  protected cancelEdit(): void {
    this.editRow.set(null);
  }

  /**
   * Zapis edycji. `edited` ustawiamy TYLKO wtedy, gdy zmieniła się kategoria — to na nim
   * opiera się status po imporcie: decyzja człowieka daje `ManuallyCategorized` i nie
   * wraca do kolejki przeglądu. Poprawka nazwy albo kwoty nie jest decyzją o kategorii.
   */
  protected saveEdit(): void {
    const row = this.editRow();
    if (!row) return;

    const categoryId = this.draftCategoryId();

    this.rows.update((rows) => rows.map((r) => r.index === row.index
      ? {
        ...this.withCategory(r, categoryId),
        description: this.draftName().trim(),
        amount: this.draftAmount(),
        date: toIsoDay(this.draftDate()) ?? r.date,
      }
      : r));

    this.editRow.set(null);
  }

  /**
   * Wiersz po wskazaniu kategorii przez CZŁOWIEKA.
   *
   * ⚠️ Zdejmuje `needsReview`, bo decyzja człowieka jest właśnie tym przeglądem — inaczej
   * wiersz z podpowiedzią, którą użytkownik świadomie poprawił, dalej straszyłby znacznikiem
   * „do weryfikacji". Zdejmuje je TYLKO razem z kategorią: wyczyszczenie pola zostawia wiersz
   * w kolejce, bo bez kategorii i tak zapisze się jako `PendingReview`.
   */
  private withCategory(row: EditableRow, categoryId: string | null): EditableRow {
    const categoryChanged = categoryId !== row.categoryId;
    const decided = row.edited || categoryChanged;

    return {
      ...row,
      categoryId,
      categoryName: this.categoryNameOf(categoryId),
      edited: decided,
      needsReview: decided && categoryId !== null ? false : row.needsReview,
    };
  }

  /**
   * Select wprost w komórce „Kategoria" — poprawka bez wchodzenia w modal edycji.
   * Ta sama logika co w `saveEdit` (kategoria + `edited`), tylko bez reszty pól:
   * inline dotyczy WYŁĄCZNIE kategorii, nazwy/kwoty/daty dalej poprawia się w modalu.
   */
  protected updateCategory(row: EditableRow, categoryId: string | null): void {
    this.rows.update((rows) => rows.map(
      (r) => (r.index === row.index ? this.withCategory(r, categoryId) : r)));
  }

  private categoryNameOf(categoryId: string | null): string | null {
    return this.sources()?.categories.find((c) => c.id === categoryId)?.name ?? null;
  }

  /** „Usuń" z menu wiersza — pojedynczy wiersz wypada z importu. */
  protected removeRow(): void {
    const row = this.menuRow();
    if (!row) return;

    this.rows.update((rows) => rows.filter((r) => r.index !== row.index));
    this.selected.update((s) => {
      const next = new Set(s);
      next.delete(row.index);
      return next;
    });
  }

  // ── Nawigacja ────────────────────────────────────────────────────────────────────────

  protected goBack(): void {
    this.error.set(null);

    if (this.step() === Step.Preview) {
      // Cofnięcie z podglądu unieważnia go razem z edycjami — inaczej po podmianie pliku
      // użytkownik zatwierdzałby poprawki naniesione na poprzedni.
      this.rows.set([]);
      this.selected.set(new Set());
      this.file.set(null);
      this.step.set(Step.File);
      return;
    }

    if (this.step() === Step.File) {
      this.step.set(Step.Source);
      return;
    }

    this.step.set(Step.Preview);
  }

  protected finish(): void {
    void this.router.navigate(['/dashboard']);
  }

  /**
   * Kolejka korekt powstanie w osobnym zadaniu. Do tego czasu mówimy o tym wprost,
   * zamiast udawać nawigację (tak samo jak kafle Szybkich akcji — `SystemAction.route`).
   */
  protected notImplemented(labelKey: string): void {
    this.message.info(
      this.translate.instant('actions.notImplemented', {
        label: this.translate.instant(labelKey),
      }),
    );
  }

  /**
   * Kolory wartosci w podsumowaniu. Kolor niesie tu znaczenie, nie dekoracje:
   * ujemny stan budzetu na zielono czytalby sie jak dobra wiadomosc.
   * Wartosci z palety wykresow (chart-palette.ts) i makiety.
   */
  protected readonly colors = {
    neutral: '#252b27',
    negative: '#e44e44',
    positive: '#51c273',
  };

  // ── Formatowanie ─────────────────────────────────────────────────────────────────────


  /**
   * Makieta pokazuje kwotę z walutą i z grupowaniem tysięcy („112,893 PLN").
   *
   * `useGrouping: true` jest tu konieczne: domyślne `auto` NIE grupuje liczb
   * czterocyfrowych w `pl`, więc obok siebie wychodziło „8857,66" i „12 345,60".
   */
  protected money(value: number): string {
    return new Intl.NumberFormat('pl-PL', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
      useGrouping: true,
    }).format(value);
  }

  /** Makieta: data jako `01.01.2026`. Backend przysyła ISO. */
  protected day(iso: string): string {
    const [y, m, d] = iso.split('-');
    return `${d}.${m}.${y}`;
  }

  /** Makieta: „Trafność" w procentach z dwoma miejscami („89.43%"). */
  protected confidence(value: number | null): string {
    if (value === null) return '—';
    return `${new Intl.NumberFormat('pl-PL', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value * 100)}%`;
  }
}
