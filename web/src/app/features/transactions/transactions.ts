import { Component, computed, effect, inject, linkedSignal, signal, untracked } from '@angular/core';
import { ActiveBudget } from '../../core/active-budget';
import { CommonModule } from '@angular/common';
import { HttpClient, httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDropdownModule } from 'ng-zorro-antd/dropdown';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTableModule, NzTableQueryParams } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { EnumTranslatePipe } from '../../core/pipes/enum-translate.pipe';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { fromIsoDate, toIsoDate } from '../../core/api/date-param';
import { CategoryOption } from '../../core/api/models/category-option';
import { TransactionListItem } from '../../core/api/models/transaction-list-item';
import { TransactionListResponse } from '../../core/api/models/transaction-list-response';
import { errorOf, valueOf } from '../../core/api/resource-value';
import { ErrorMessages } from '../../core/errors/error-messages';
import { DraftRow } from './draft-row';
import { parseAmount } from '../../core/parse-amount';

/** Wartość selecta kategorii dla wierszy bez kategorii — myliłaby się z „nic nie wybrano". */
const NoCategory = '__brak__';

/**
 * Wartość `?origin=` przy wejściu z zakładki „Dane treningowe" — patrz `crumbs`.
 *
 * ⚠️ `origin`, NIE `from`. Ten ekran ma już parametr `from` — jest nim POCZĄTEK ZAKRESU DAT,
 * wysyłany do API jako `DateOnly`. Znacznik pochodzenia pod tą samą nazwą wchodził więc
 * do zapytania jako data: `?from=training` nie wiązało się do `DateOnly` i cała lista
 * kończyła się odpowiedzią 400.
 */
const OriginTraining = 'training';

/** Wartość `?origin=` przy wejściu z „Przejdź do powiązanych” na ekranie zleceń stałych. */
const OriginStandingOrders = 'standing-orders';

/**
 * Jeden element ścieżki breadcrumbów. `label` to KLUCZ tłumaczenia, nie gotowy tekst:
 * ścieżka przelicza się przy zmianie języka razem z resztą ekranu.
 */
interface Crumb {
  label: string;
  icon?: string;
  /** Brak = element bieżący, czyli ten ekran — nie jest odnośnikiem. */
  link?: string;
  queryParams?: Record<string, string>;
}

/**
 * Musi zgadzać się z `TransactionLimits.DescriptionMaxLength` w API (i z `HasMaxLength(500)`
 * w EF). Bez limitu po tej stronie za długi opis rozbijałby się dopiero o `varchar(500)`
 * w Postgresie — czyli 500 zamiast komunikatu przy polu.
 */
const DescriptionMaxLength = 500;

/** Najdłuższa nazwa zlecenia epizodycznego — `HasMaxLength(100)` w API. */
const EPISODIC_NAME_MAX = 100;

/**
 * Dokładnie te wartości, których backend oczekuje w query stringu — wiązanie enuma
 * w minimalnym API jest wrażliwe na wielkość liter ("all" dawało 400, "All" nie).
 * Ta sama zasada co przy `ALL_STATUSES` niżej: front wysyła nazwy enuma 1:1.
 */
type Direction = 'All' | 'Expense' | 'Income';

/** Te same pięć wartości co `TransactionStatus` w API — patrz enums.transactionStatus w i18n. */
const ALL_STATUSES = ['Imported', 'AutoCategorized', 'PendingReview', 'ManuallyCategorized', 'Confirmed'];

/** Odwzorowanie statusu na wariant `nz-badge` — znaczenie niesie kolor, nie dekoracja. */
const STATUS_BADGE: Record<string, string> = {
  Imported: 'default',
  PendingReview: 'processing',
  AutoCategorized: 'warning',
  ManuallyCategorized: 'success',
  Confirmed: 'success',
};

/** Kryteria filtrowania — dokładny odpowiednik `TransactionFilter` z API. */
interface FilterPayload {
  budgetIds: string[] | null;
  from: string | null;
  to: string | null;
  categoryId: string | null;
  uncategorized: boolean;
  direction: Direction;
  status: string[] | null;
  amountFrom: number | null;
  amountTo: number | null;
  search: string | null;
  standingOrderId: string | null;
}

interface SelectionPayload {
  ids: string[] | null;
  filter: FilterPayload | null;
}

@Component({
  selector: 'app-transactions',
  imports: [
    CommonModule, FormsModule, RouterLink,
    NzAlertModule, NzBadgeModule, NzBreadCrumbModule, NzButtonModule, NzDatePickerModule,
    NzDropdownModule, NzEmptyModule, NzIconModule, NzInputModule, NzInputNumberModule,
    NzModalModule, NzSelectModule, NzSpinModule, NzStatisticModule, NzTableModule, NzTagModule,
    TranslatePipe, EnumTranslatePipe,
  ],
  templateUrl: './transactions.html',
  styleUrl: './transactions.scss',
})
export class Transactions {
  /** Parser polskiego formatu kwot dla pól `nz-input-number` — uzasadnienie przy `parseAmount`. */
  protected readonly parseAmount = parseAmount;

  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly message = inject(NzMessageService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly errorMessages = inject(ErrorMessages);

  // ── Filtry: URL jest jedynym źródłem prawdy (plan „lista transakcji", punkt 7) ─────────
  // Jednokierunkowy przepływ: URL → sygnały → httpResource. Zapis do URL wyłącznie
  // z akcji użytkownika (changeQuery), nigdy odwrotnie — inaczej dwustronne wiązanie
  // zapętliłoby się przy pierwszym przeładowaniu.

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  private readonly activeBudget = inject(ActiveBudget);

  /**
   * Budżety wybrane w multiselekcie. Nazwa parametru została w liczbie pojedynczej
   * (`?budgetId=…&budgetId=…`), żeby stare adresy z jednym budżetem — odnośniki z dashboardu,
   * zakładki użytkownika — działały dalej bez zmian.
   */
  private readonly budgetIdsFromUrl = computed(() => this.queryParams().getAll('budgetId'));

  /**
   * ⚠️ Wyjątek od „URL jest jedynym źródłem prawdy" — i tylko dla budżetu. Gdy adres NIE podaje
   * budżetu, lista liczy na budżecie z widoku (`ActiveBudget`), a nie na domyślnym z backendu.
   * Inaczej wejście z nagłówka albo z ekranu oszczędności pokazywało transakcje innego budżetu niż
   * ten, który użytkownik właśnie oglądał (issue #16). Adres z `budgetId` dalej wygrywa, więc
   * zakładki i udostępnione linki działają jak dotąd.
   */
  protected readonly budgetIds = computed(() => [...this.activeBudget.resolve(this.budgetIdsFromUrl())]);

  /** Zmiana filtra budżetów (zapisywana do adresu) ustawia budżet w widoku dla kolejnych ekranów. */
  private readonly publishBudgetFromUrl = effect(() => {
    const fromUrl = this.budgetIdsFromUrl();
    if (fromUrl.length > 0) this.activeBudget.set(fromUrl);
  });
  protected readonly from = computed(() => this.queryParams().get('from'));
  protected readonly to = computed(() => this.queryParams().get('to'));
  protected readonly categoryId = computed(() => this.queryParams().get('categoryId'));
  protected readonly uncategorized = computed(() => this.queryParams().get('uncategorized') === 'true');
  protected readonly direction = computed<Direction>(
    () => (this.queryParams().get('direction') as Direction | null) ?? 'All',
  );
  protected readonly statusFilter = computed(() => this.queryParams().getAll('status'));
  protected readonly amountFrom = computed(() => toNumberOrNull(this.queryParams().get('amountFrom')));
  protected readonly amountTo = computed(() => toNumberOrNull(this.queryParams().get('amountTo')));
  protected readonly search = computed(() => this.queryParams().get('search') ?? '');

  /** Zlecenie stałe z adresu — tylko transakcje do niego przypięte („Przejdź do powiązanych”). */
  protected readonly standingOrderId = computed(() => this.queryParams().get('standingOrderId'));
  protected readonly sort = computed(() => this.queryParams().get('sort') ?? 'date');
  protected readonly desc = computed(() => this.queryParams().get('desc') !== 'false');
  protected readonly page = computed(() => Number(this.queryParams().get('page') ?? '1'));
  protected readonly pageSize = computed(() => Number(this.queryParams().get('pageSize') ?? '10'));

  /**
   * Opcje filtra kolumny „Kategoria" (`nzFilters` w nagłówku, tak jak w imporcie) —
   * `byDefault` odzwierciedla aktualny stan z URL, żeby zaznaczenie w dropdownie
   * nie rozjechało się z filtrem po zmianie z zewnątrz (np. „Wyczyść filtry").
   */
  protected readonly categoryFilterOptions = computed(() => {
    const current = this.uncategorized() ? NoCategory : this.categoryId();
    return [
      {
        text: this.translate.instant('transactions.filters.uncategorized'),
        value: NoCategory,
        byDefault: current === NoCategory,
      },
      ...this.categories().map((c) => ({ text: c.name, value: c.id, byDefault: current === c.id })),
    ];
  });

  /** Opcje filtra kolumny „Status" — te same pięć wartości, wielokrotny wybór. */
  protected readonly statusFilterOptions = computed(() => {
    const selected = new Set(this.statusFilter());
    return ALL_STATUSES.map((s) => ({
      text: this.translate.instant(`enums.transactionStatus.${s}`),
      value: s,
      byDefault: selected.has(s),
    }));
  });

  /**
   * Ścieżka breadcrumbów. Zależy od tego, SKĄD użytkownik tu przyszedł (`?from=`), i od
   * niczego więcej — to jedyne zastosowanie tego parametru, nie jest filtrem.
   *
   * Ten sam ekran jest wejściem z dwóch miejsc: z dashboardu (klik w słupek kategorii)
   * i z zakładki „Dane treningowe" (klik w „Przejrzyj je"). Sztywne „Dashboard / Lista
   * transakcji" w tym drugim przypadku kłamało i odsyłało o dwa ekrany w bok.
   */
  protected readonly crumbs = computed<Crumb[]>(() => {
    const list: Crumb = {
      label: 'transactions.title',
      icon: 'icons/lista-transakcji-azure.svg',
    };

    if (this.queryParams().get('origin') === OriginStandingOrders) {
      return [
        { label: 'standingOrders.title', link: '/standing-orders' },
        list,
      ];
    }

    if (this.queryParams().get('origin') === OriginTraining) {
      return [
        { label: 'settings.breadcrumb', link: '/settings' },
        // Bez `?tab=training` ten odnośnik wracałby na ustawienia otwarte na budżetach.
        { label: 'settings.tabs.training', link: '/settings', queryParams: { tab: 'training' } },
        list,
      ];
    }

    return [
      { label: 'dashboard.breadcrumb', link: '/dashboard', icon: 'icons/dashboard-azure.svg' },
      list,
    ];
  });

  /**
   * Dozwolone rozmiary strony. Górna wartość MUSI mieścić się w limicie serwera
   * (`GetTransactionsListQueryHandler.MaxPageSize` = 200) — inaczej wybór 500 zostałby po cichu
   * przycięty i tabela pokazałaby inną liczbę wierszy, niż mówi selektor.
   */
  protected readonly pageSizeOptions = [10, 20, 50, 100];

  protected sortOrderFor(field: 'date' | 'amount'): 'ascend' | 'descend' | null {
    return this.sort() === field ? (this.desc() ? 'descend' : 'ascend') : null;
  }

  // ── Filtr „Kwota" (kierunek + zakres) — jedyny bez naturalnej kolumny wg makiety,
  // więc żyje jako własny dropdown na nagłówku „Kwota" (nzCustomFilter), tak jak
  // RangeFilter w ustawieniach, tylko z dodatkowym selectem kierunku.

  protected readonly amountFilterVisible = signal(false);
  protected readonly amountFilterActive = computed(
    () => this.direction() !== 'All' || this.amountFrom() !== null || this.amountTo() !== null,
  );
  protected readonly directionDraft = signal<Direction>('All');
  protected readonly amountFromDraft = signal<number | null>(null);
  protected readonly amountToDraft = signal<number | null>(null);

  protected openAmountFilter(visible: boolean): void {
    this.amountFilterVisible.set(visible);
    if (visible) {
      this.directionDraft.set(this.direction());
      this.amountFromDraft.set(this.amountFrom());
      this.amountToDraft.set(this.amountTo());
    }
  }

  protected applyAmountFilter(): void {
    this.amountFilterVisible.set(false);
    void this.changeQuery({
      direction: this.directionDraft() === 'All' ? null : this.directionDraft(),
      amountFrom: this.amountFromDraft(),
      amountTo: this.amountToDraft(),
      page: 1,
    });
  }

  protected resetAmountFilter(): void {
    this.directionDraft.set('All');
    this.amountFromDraft.set(null);
    this.amountToDraft.set(null);
    this.amountFilterVisible.set(false);
    void this.changeQuery({ direction: null, amountFrom: null, amountTo: null, page: 1 });
  }

  // ── Dane ─────────────────────────────────────────────────────────────────────────────

  private readonly list = httpResource<TransactionListResponse>(() => ({
    url: '/api/transactions',
    params: {
      ...(this.budgetIds().length > 0 ? { budgetId: this.budgetIds() } : {}),
      ...(this.from() ? { from: this.from()! } : {}),
      ...(this.to() ? { to: this.to()! } : {}),
      ...(this.categoryId() ? { categoryId: this.categoryId()! } : {}),
      uncategorized: this.uncategorized(),
      direction: this.direction(),
      ...(this.statusFilter().length > 0 ? { status: this.statusFilter() } : {}),
      ...(this.amountFrom() !== null ? { amountFrom: this.amountFrom()! } : {}),
      ...(this.amountTo() !== null ? { amountTo: this.amountTo()! } : {}),
      ...(this.search() ? { search: this.search() } : {}),
      ...(this.standingOrderId() ? { standingOrderId: this.standingOrderId()! } : {}),
      page: this.page(),
      pageSize: this.pageSize(),
      sort: this.sort(),
      desc: this.desc(),
    },
  }));

  /**
   * ⚠️ `valueOf`, nie `this.list.value()` — w stanie błędu ten drugi RZUCA, a nie zwraca
   * `undefined`. Bezpośredni odczyt wywalał render szablonu przy odpowiedzi 400 i zostawiał
   * `nz-spin` zawieszony nad pustą tabelą. Patrz `core/api/resource-value.ts`.
   */
  private readonly freshListValue = valueOf(this.list);

  /**
   * Ostatnia odpowiedź listy — trzymana PRZEZ przeładowanie (zgłoszenie #17).
   *
   * `httpResource` na czas nowego żądania zwraca `undefined`, więc każda zmiana filtra zerowała tabelę, kafle
   * i przełącznik budżetu, a po odpowiedzi rysowała je od nowa — ekran skakał. Teraz stare wiersze stoją pod
   * spinnerem, dopóki API nie zwróci nowego widoku. Błąd ma własny stan (`listError`), więc nie zasłoni go
   * poprzednia odpowiedź.
   */
  private readonly listValue = linkedSignal<TransactionListResponse | undefined, TransactionListResponse | undefined>({
    source: this.freshListValue,
    computation: (next, prev) => next ?? prev?.value,
  });

  /** Błąd listy — osobny stan niż „brak wyników", patrz szablon. */
  protected readonly listError = errorOf(this.list);

  /** Zdanie z API dla błędu listy; wspólne dla całej aplikacji (patrz `core/errors`). */
  protected errorText(error: unknown): string {
    return this.errorMessages.of(error);
  }

  /**
   * Ponowna próba tym samym żądaniem — na awarię przejściową (500, zerwana sieć).
   *
   * ⚠️ Nie pomoże na 400, bo ten bierze się z FILTRA w adresie i powtórzy się identycznie.
   * Dlatego obok stoi „Wyczyść filtry": to jedyne wyjście z listy, której adres jest zepsuty
   * (np. `?direction=all` małą literą — wiązanie enuma jest wrażliwe na wielkość liter).
   */
  protected reload(): void {
    this.list.reload();
  }

  protected readonly loading = this.list.isLoading;
  protected readonly items = computed(() => this.listValue()?.items ?? []);
  protected readonly total = computed(() => this.listValue()?.total ?? 0);

  /**
   * Kafle „Podsumowanie" — te same wielkości co na dashboardzie, ale dla zestawu wynikającego
   * z FILTRÓW TABELI, z pominięciem stronicowania. Liczy je serwer w tej samej odpowiedzi
   * (`TransactionSummary`), bo klient trzyma jedną stronę i sam by nie umiał; osobne zapytanie
   * byłoby za to zbędne — filtr jest ten sam, więc agregaty jadą razem z listą.
   */
  protected readonly summary = computed(() => this.listValue()?.summary ?? null);

  /** Budżety do multiselecta — jadą razem z listą, patrz `TransactionListResponse.Budgets`. */
  protected readonly budgetOptions = computed(() => this.listValue()?.budgets ?? []);

  /**
   * Co ma być zaznaczone w kontrolce.
   *
   * ⚠️ Źródłem jest ODPOWIEDŹ SERWERA, nie sam adres. Gdy użytkownik nie wybrał nic, serwer
   * bierze budżet domyślny i mówi który — kontrolka pokazuje go wtedy zaznaczonego, zamiast
   * świecić pustką przy liście, która jednak jest z konkretnego budżetu. Dopóki odpowiedź nie
   * przyszła, pokazujemy to, co w adresie, żeby wybór nie migotał.
   */
  protected readonly selectedBudgetIds = computed(
    () => this.listValue()?.selectedBudgetIds ?? this.budgetIds(),
  );

  /**
   * Zapis wyboru do adresu. Pusty wybór kasuje parametr — wtedy o budżecie znów decyduje
   * serwer, tą samą regułą co dashboard.
   */
  protected setBudgets(ids: string[]): void {
    void this.changeQuery({ budgetId: ids.length > 0 ? ids : null, page: 1 });
  }

  /**
   * Pierwszy pusty stan: w bazie nie ma ŻADNEGO budżetu.
   *
   * ⚠️ Pusta lista `selectedBudgetIds` znaczy dokładnie to i nic więcej. Nie może wynikać
   * z pustego wyboru w multiselekcie — przy pustym wyborze serwer bierze budżet domyślny
   * i zwraca go tutaj, więc lista jest pusta wyłącznie wtedy, gdy nie było z czego wybierać.
   */
  protected readonly noBudget = computed(() => {
    const v = this.listValue();
    return v !== undefined && v.selectedBudgetIds.length === 0;
  });

  /** Drugi pusty stan: budżety są, ale nie mają ŻADNEJ transakcji. */
  protected readonly noTransactionsAtAll = computed(() => {
    const v = this.listValue();
    return v !== undefined && v.selectedBudgetIds.length > 0 && !v.hasAnyTransactions;
  });

  /** Trzeci pusty stan: transakcje są, ale żadna nie pasuje do filtra. */
  protected readonly noFilterResults = computed(() => {
    const v = this.listValue();
    return v !== undefined && v.selectedBudgetIds.length > 0 && v.hasAnyTransactions && v.total === 0;
  });

  private readonly categoriesResource = httpResource<CategoryOption[]>(() => '/api/categories');
  private readonly categoriesValue = valueOf(this.categoriesResource);
  protected readonly categories = computed(() => this.categoriesValue() ?? []);

  /** Wcielenie tabeli, którego echo inicjalizacyjne już odrzuciliśmy — patrz `onQueryParamsChange`. */
  private tableInstance: unknown = null;

  // ── Zaznaczenie: dwa zasięgi, nie żyje w URL (stan czysto interakcyjny) ─────────────────

  protected readonly selectedIds = signal<ReadonlySet<string>>(new Set());
  protected readonly selectAllMatching = signal(false);

  /**
   * Zmiana budżetu MUSI porzucić zaznaczenie i edycję — inaczej zostają identyfikatory
   * wierszy, których użytkownik już nie widzi.
   *
   * `changeQuery` nie wystarczy: czyści stan tylko przy nawigacji wywołanej PRZEZ TEN ekran,
   * a budżet zmienia się też z zewnątrz (link z dashboardu, cofnięcie w przeglądarce,
   * ręczna poprawka adresu). Router przy zmianie samych parametrów zapytania NIE tworzy
   * komponentu od nowa, więc bez tego efektu `selectedIds` przeżywało podmianę budżetu:
   * licznik zaznaczenia pokazywał „5", żaden widoczny wiersz nie był zaznaczony, a akcja
   * masowa poszłaby na wiersze z poprzedniego budżetu. Serwer to dziś odrzuca (zasięg filtra
   * w `TransactionSelection`), ale użytkownik nie ma powodu oglądać takiego błędu.
   */
  private readonly resetOnBudgetChange = effect(() => {
    // Zależność po WARTOŚCI, nie po referencji: `getAll` zwraca nową tablicę na każdy odczyt,
    // więc porównanie referencji kasowałoby zaznaczenie przy każdym przeliczeniu sygnału.
    this.budgetIds().join(',');
    untracked(() => {
      this.clearSelection();
      this.cancelEdit();
    });
  });

  protected readonly allOnPageSelected = computed(() => {
    const items = this.items();
    return items.length > 0 && items.every((i) => this.selectedIds().has(i.id));
  });

  /** Przycisk „zaznacz wszystkie N pasujących" ma sens tylko, gdy strona to nie WSZYSTKO. */
  protected readonly canSelectAllMatching = computed(() =>
    this.allOnPageSelected() && !this.selectAllMatching() && this.total() > this.items().length,
  );

  protected readonly selectionCount = computed(
    () => (this.selectAllMatching() ? this.total() : this.selectedIds().size),
  );

  protected toggleRow(id: string, checked: boolean): void {
    const next = new Set(this.selectedIds());
    if (checked) next.add(id); else next.delete(id);
    this.selectedIds.set(next);
    this.selectAllMatching.set(false);
  }

  protected toggleAllOnPage(checked: boolean): void {
    const next = new Set(this.selectedIds());
    for (const item of this.items()) {
      if (checked) next.add(item.id); else next.delete(item.id);
    }
    this.selectedIds.set(next);
    this.selectAllMatching.set(false);
  }

  protected selectAllMatchingFilter(): void {
    this.selectedIds.set(new Set(this.items().map((i) => i.id)));
    this.selectAllMatching.set(true);
  }

  private clearSelection(): void {
    this.selectedIds.set(new Set());
    this.selectAllMatching.set(false);
  }

  private currentFilter(): FilterPayload {
    return {
      budgetIds: this.budgetIds().length > 0 ? this.budgetIds() : null,
      from: this.from(),
      to: this.to(),
      categoryId: this.categoryId(),
      uncategorized: this.uncategorized(),
      direction: this.direction(),
      status: this.statusFilter().length > 0 ? this.statusFilter() : null,
      amountFrom: this.amountFrom(),
      amountTo: this.amountTo(),
      search: this.search() || null,
      standingOrderId: this.standingOrderId(),
    };
  }

  /**
   * Filtr jedzie ZAWSZE, także przy jawnym zaznaczeniu — wyznacza budżet, w którym serwerowi
   * wolno działać (patrz `TransactionSelection` po stronie API). `ids` tylko go zawęża.
   */
  private buildSelection(): SelectionPayload {
    return this.selectAllMatching()
      ? { ids: null, filter: this.currentFilter() }
      : { ids: [...this.selectedIds()], filter: this.currentFilter() };
  }

  /** Zaznaczenie jednego wiersza z menu „⋮" — ten sam zasięg co przy akcjach z toolbara. */
  private singleRowSelection(id: string): SelectionPayload {
    return { ids: [id], filter: this.currentFilter() };
  }

  // ── Edycja inline: pojedyncza i masowa tym samym mechanizmem ────────────────────────────
  //
  // „Zaznacz wszystkie N pasujących" (selectAllMatching) NIE dostaje edycji inline —
  // edytuje się to, co się widzi (plan, punkt 13). Przycisk „Edytuj zaznaczone" jest
  // wtedy wyłączony w szablonie.

  protected readonly editingIds = signal<ReadonlySet<string>>(new Set());
  protected readonly drafts = signal<Readonly<Record<string, DraftRow>>>({});
  protected readonly saving = signal(false);

  private startEdit(ids: readonly string[]): void {
    const next = { ...this.drafts() };
    for (const id of ids) {
      const item = this.items().find((i) => i.id === id);
      if (!item) continue;
      next[id] = {
        date: fromIsoDate(item.date),
        description: item.description,
        amount: item.amount,
        categoryId: item.categoryId,
      };
    }
    this.drafts.set(next);
    this.editingIds.set(new Set(ids));
  }

  protected editSelected(): void {
    this.startEdit([...this.selectedIds()]);
  }

  protected updateDraft(id: string, patch: Partial<DraftRow>): void {
    this.drafts.update((d) => ({ ...d, [id]: { ...d[id], ...patch } }));
  }

  /**
   * Co jest nie tak z wierszem — `null`, gdy da się go zapisać.
   *
   * Kwota `null` NIE jest podmieniana na zero. Wcześniej szablon robił `$event ?? 0`, więc
   * wyczyszczenie pola (żeby wpisać nową kwotę) zamieniało transakcję na 0,00 PLN — cicho,
   * bez śladu, i taka trafiała do zapisu, jeśli użytkownik w międzyczasie kliknął gdzie
   * indziej. W aplikacji o pieniądzach puste pole musi blokować zapis, a nie zgadywać.
   */
  protected errorFor(id: string): 'amount' | 'description' | 'descriptionTooLong' | null {
    const draft = this.drafts()[id];
    if (!draft) return null;

    if (draft.amount === null) return 'amount';
    if (draft.description.trim().length === 0) return 'description';
    if (draft.description.trim().length > DescriptionMaxLength) return 'descriptionTooLong';
    return null;
  }

  /** Zapis wolno puścić dopiero, gdy KAŻDY edytowany wiersz jest kompletny. */
  protected readonly editHasErrors = computed(
    () => [...this.editingIds()].some((id) => this.errorFor(id) !== null),
  );

  protected cancelEdit(): void {
    this.editingIds.set(new Set());
    this.drafts.set({});
  }

  protected async saveEdits(): Promise<void> {
    const ids = [...this.editingIds()];
    if (ids.length === 0) return;

    // Strażnik, nie tylko wygaszony przycisk: `saveEdits` woła też Enter w polach formularza.
    if (this.editHasErrors()) return;

    const drafts = this.drafts();
    this.saving.set(true);
    try {
      await firstValueFrom(this.http.patch('/api/transactions', {
        // Budżet jedzie razem z edycją — serwer sprawdza, czy wiersz do niego należy,
        // zamiast ufać samym identyfikatorom (patrz UpdateTransactionsRequest w API).
        budgetIds: this.budgetIds().length > 0 ? this.budgetIds() : null,
        edits: ids.map((id) => {
          const d = drafts[id];
          return {
            id,
            date: toIsoDate(d.date),
            description: d.description.trim(),
            amount: d.amount,
            categoryId: d.categoryId,
          };
        }),
      }));

      this.message.success(this.translate.instant('transactions.edit.saved', { count: ids.length }));
      this.editingIds.set(new Set());
      this.drafts.set({});
      this.clearSelection();
      this.list.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.saving.set(false);
    }
  }

  // ── Menu wiersza (⋮) ─────────────────────────────────────────────────────────────────
  //
  // Makieta: klik w „⋮" otwiera menu (Edytuj / Oznacz jako zlecenie epizodyczne / Usuń), a nie
  // wchodzi od razu w edycję. Wiersz pod kursorem zapamiętujemy przy otwarciu menu —
  // ten sam wzorzec co w kroku 3 importu.

  protected readonly menuRow = signal<TransactionListItem | null>(null);

  protected editMenuRow(): void {
    const row = this.menuRow();
    if (row) this.startEdit([row.id]);
  }

  protected deleteMenuRow(): void {
    const row = this.menuRow();
    if (row) void this.bulkDelete(this.singleRowSelection(row.id), 1);
  }

  // ── Zlecenie epizodyczne z wiersza (makieta 234:1014) ────────────────────────────────
  //
  // Zastępuje flagę „duży wydatek”. Bez akcji masowej: każde zlecenie potrzebuje własnej nazwy.

  protected readonly episodicOpen = signal(false);
  protected readonly episodicRow = signal<TransactionListItem | null>(null);
  protected readonly episodicName = signal('');
  protected readonly episodicDescription = signal('');

  /** Wiersz ze zleceniem proponuje przejście do niego, bez zleceniem — założenie; nigdy oba naraz. */
  protected episodicMenuRow(): void {
    const row = this.menuRow();
    if (!row) return;
    if (row.episodicOrderId) {
      void this.router.navigate(['/episodic-orders'], { queryParams: { tab: 'realized' } });
      return;
    }
    this.episodicRow.set(row);
    // Podpowiedź z tytułu przelewu — i tak do poprawienia, ale pusta nazwa to kolejny krok do zrobienia.
    this.episodicName.set(row.description.slice(0, EPISODIC_NAME_MAX));
    this.episodicDescription.set('');
    this.episodicOpen.set(true);
  }

  protected async saveEpisodic(): Promise<void> {
    const row = this.episodicRow();
    const name = this.episodicName().trim();
    if (!row || name.length === 0) return;

    try {
      // Bez budżetu — serwer bierze budżet TRANSAKCJI, bo lista pokazuje kilka budżetów naraz.
      await firstValueFrom(this.http.post('/api/episodic-orders', {
        budgetId: null,
        name,
        description: this.episodicDescription().trim() || null,
        transactionId: row.id,
        categoryId: null,
        amount: null,
        dueMonth: null,
      }));
      this.episodicOpen.set(false);
      this.message.success(this.translate.instant('transactions.episodic.saved', { name }));
      this.list.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    }
  }

  // ── Transfer do budżetu oszczędnościowego (#10) ─────────────────────────────────────
  //
  // Bez makiety — propozycja Figma (node-id=243-8027). „Odepnij" pokazuje się w menu
  // TYLKO na wierszach już sparowanych, wzorem „Przejdź do powiązanych"/„Oznacz jako zlecenie".

  protected unpinSavingsTransferMenuRow(): void {
    const row = this.menuRow();
    if (row) void this.unpinSavingsTransfer(row.id);
  }

  private async unpinSavingsTransfer(id: string): Promise<void> {
    try {
      await firstValueFrom(this.http.delete(`/api/budgets/savings-transfer/pins/${id}`));
      this.message.success(this.translate.instant('transactions.savingsTransfer.unpinned'));
      this.list.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    }
  }

  // ── Akcje masowe ─────────────────────────────────────────────────────────────────────
  //
  // Te same wywołania obsługują zaznaczenie z toolbara i pojedynczy wiersz z menu „⋮" —
  // różni je tylko zasięg podany w argumencie.

  protected async bulkDelete(
    selection: SelectionPayload = this.buildSelection(),
    count: number = this.selectionCount(),
  ): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      header: this.translate.instant('transactions.bulkDelete.confirm', { count }),
      danger: true,
    });
    if (!ok) return;

    await firstValueFrom(this.http.post('/api/transactions/bulk-delete', { selection }));
    this.message.success(this.translate.instant('transactions.bulkDelete.success', { count }));
    this.clearSelection();
    this.list.reload();
  }

  /** Modal „Masowe ustawianie kategorii" — wzorzec z makiety (Figma 124:7852). */
  protected readonly categoryModalOpen = signal(false);
  protected readonly categoryModalDraft = signal<string | null>(null);

  protected openCategoryModal(): void {
    this.categoryModalDraft.set(null);
    this.categoryModalOpen.set(true);
  }

  protected async confirmCategoryModal(): Promise<void> {
    const categoryId = this.categoryModalDraft();
    if (!categoryId) return;

    const count = this.selectionCount();
    await firstValueFrom(this.http.post('/api/transactions/bulk-set-category', {
      selection: this.buildSelection(),
      categoryId,
    }));
    this.message.success(this.translate.instant('transactions.bulkSetCategory.success', { count }));
    this.categoryModalOpen.set(false);
    this.clearSelection();
    this.list.reload();
  }

  // ── Filtry i sortowanie: każda zmiana idzie przez ten sam strażnik ──────────────────────

  /**
   * Jedyna droga zapisu do URL. Jeśli trwa edycja inline, dopytuje przed porzuceniem —
   * zmiana strony/filtra/sortowania przeładowuje dane z serwera i zgubiłaby niezapisane
   * kontrolki (ryzyko z planu „lista transakcji").
   */
  private async changeQuery(
    patch: Record<string, string | number | boolean | string[] | null>,
  ): Promise<void> {
    if (this.editingIds().size > 0) {
      const ok = await this.confirmDialog.confirm({
        header: this.translate.instant('transactions.discardEdits.header'),
        description: this.translate.instant('transactions.discardEdits.description'),
      });
      if (!ok) return;
      this.cancelEdit();
    }

    this.clearSelection();
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: patch,
      queryParamsHandling: 'merge',
    });
  }

  protected setSearch(value: string): void {
    void this.changeQuery({ search: value || null, page: 1 });
  }

  /** Zakres dat z kontrolki obok wyszukiwarki — ten sam `from`/`to` w adresie, który niesie wejście z dashboardu. */
  protected readonly dateRange = computed<Date[]>(() => {
    const from = this.from();
    const to = this.to();
    return from && to ? [fromIsoDate(from), fromIsoDate(to)] : [];
  });

  /** Wyczyszczenie kontrolki zdejmuje OBA końce — pół zakresu z adresu nie miałoby na ekranie reprezentacji. */
  protected setDateRange(range: (Date | null)[] | null): void {
    const [from, to] = range ?? [];
    void this.changeQuery({
      from: from ? toIsoDate(from) : null,
      to: to ? toIsoDate(to) : null,
      page: 1,
    });
  }

  protected clearFilters(): void {
    void this.changeQuery({
      categoryId: null, uncategorized: null, direction: null, status: null,
      amountFrom: null, amountTo: null, search: null, standingOrderId: null, from: null, to: null, page: 1,
    });
  }

  /** ✕ na etykiecie filtra zlecenia. Okruszek pochodzenia zostaje — powrót dalej prowadzi do zleceń. */
  protected clearStandingOrder(): void {
    void this.changeQuery({ standingOrderId: null, page: 1 });
  }

  /** Nazwa zlecenia do etykiety filtra — z odpowiedzi listy, bo adres niesie tylko identyfikator. */
  protected readonly standingOrderName = computed(() => this.listValue()?.standingOrderName ?? null);

  /**
   * Jedno zdarzenie z `nz-table` niesie KOMPLET stanu (strona, sortowanie, filtry kolumn
   * kategorii/statusu) za każdym razem — tak jak w imporcie, tylko tam robi to lokalnie na
   * tablicy w pamięci, a tu każda zmiana leci do serwera. `nzColumnKey` identyfikuje kolumnę.
   */
  protected onQueryParamsChange(params: NzTableQueryParams, table: unknown): void {
    // NG-ZORRO emituje `nzQueryParams` RAZ zaraz po zainicjowaniu tabeli — to echo jej
    // WŁASNEGO, jeszcze pustego stanu filtrów, a nie decyzja użytkownika. Zapisane do URL
    // kasowało `categoryId`/`status` z adresu: dokładnie tak ginął filtr przy wejściu
    // z wykresu na dashboardzie (adres był poprawny przez ułamek sekundy, po czym tabela
    // nadpisywała go pustką).
    //
    // Rozpoznajemy echo po TOŻSAMOŚCI TABELI, nie po liczniku wywołań: tabela znika i wraca
    // razem z pustymi stanami, a każde takie wcielenie emituje własne echo.
    if (this.tableInstance !== table) {
      this.tableInstance = table;
      return;
    }

    const activeSort = params.sort.find((s) => s.value !== null) ?? null;

    const categoryFilterValue = params.filter.find((f) => f.key === 'category')?.value;
    const categorySelected = (toArray(categoryFilterValue)[0] as string | undefined) ?? null;

    const statusFilterValue = params.filter.find((f) => f.key === 'status')?.value;
    const statusSelected = toArray(statusFilterValue) as string[];

    // Zmiana rozmiaru strony wraca na PIERWSZĄ. Numer strony po przeliczeniu znaczy co innego
    // niż przed: kto oglądał stronę 8 po 10 wierszy, po przełączeniu na 100 wylądowałby
    // na stronie 8 z ośmiuset — czyli na pustej tabeli.
    const pageSizeChanged = params.pageSize !== this.pageSize();

    void this.changeQuery({
      page: pageSizeChanged ? 1 : params.pageIndex,
      pageSize: params.pageSize,
      sort: activeSort?.key ?? null,
      desc: activeSort ? activeSort.value === 'descend' : null,
      categoryId: categorySelected !== null && categorySelected !== NoCategory ? categorySelected : null,
      uncategorized: categorySelected === NoCategory ? true : null,
      status: statusSelected.length > 0 ? statusSelected : null,
    });
  }

  // ── Formatowanie ─────────────────────────────────────────────────────────────────────

  protected statusBadge(status: string): string {
    return STATUS_BADGE[status] ?? 'default';
  }

  /** Kolory z palety wykresów/importu — ujemne wyróżnione tak jak na dashboardzie. */
  protected amountColor(amount: number): string {
    return amount < 0 ? '#e44e44' : '#252b27';
  }

  protected money(value: number): string {
    return new Intl.NumberFormat('pl-PL', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
      useGrouping: true,
    }).format(value);
  }

  protected day(iso: string): string {
    const [y, m, d] = iso.split('-');
    return `${d}.${m}.${y}`;
  }

}

function toNumberOrNull(value: string | null): number | null {
  if (value === null || value === '') return null;
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

/** `nzFilters` zwraca wartość jako tablicę albo pojedynczą wartość zależnie od trybu. */
function toArray(value: unknown): unknown[] {
  if (value === null || value === undefined) return [];
  return Array.isArray(value) ? value : [value];
}
