import { Component, computed, inject, signal } from '@angular/core';
import { ActiveBudget } from '../../core/active-budget';
import { HttpClient, httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDropdownModule } from 'ng-zorro-antd/dropdown';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  BudgetListItem,
  BudgetListResponse,
  BudgetStatus,
  TitleAmountRule,
} from '../../core/api/models/budget-list-item';
import { NzBreadCrumbComponent, NzBreadCrumbItemComponent } from 'ng-zorro-antd/breadcrumb';
import { normalizeText } from '../../core/normalize-text';
import { RangeFilter } from '../../core/range-filter/range-filter';
import { RangeFilterState } from '../../core/range-filter/range-filter-state';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { ErrorMessages } from '../../core/errors/error-messages';
import { ActivatedRoute, Router } from '@angular/router';
import { PageHeader } from '../../core/page-header/page-header';
import { Rules } from './rules/rules';
import { Training } from './training/training';
import { AccountSecurity } from './account-security/account-security';
import { valueOf } from '../../core/api/resource-value';
import { parseAmount } from '../../core/parse-amount';

/**
 * Wartości `?tab=` dla zakładek innych niż domyślna.
 * Budżety są domyślne, więc nie mają swojej: adres bez parametru znaczy „budżety".
 */
const TrainingTab = 'training';
const TrainingTabIndex = 1;
const RulesTab = 'rules';
const RulesTabIndex = 2;
const AccountTab = 'account';
const AccountTabIndex = 3;

/** Zakładka wskazana w adresie, albo 0 (budżety), gdy parametru nie ma lub jest nieznany. */
const TabIndexByParam: Record<string, number> = {
  [TrainingTab]: TrainingTabIndex,
  [RulesTab]: RulesTabIndex,
  [AccountTab]: AccountTabIndex,
};

/** Parametr adresu dla indeksu zakładki, albo `null` dla domyślnej. */
const TabParamByIndex: Record<number, string> = {
  [TrainingTabIndex]: TrainingTab,
  [RulesTabIndex]: RulesTab,
  [AccountTabIndex]: AccountTab,
};

/**
 * Który WŁASNY modal jest otwarty. Usunięcie i wyłączenie NIE są tutaj — idą przez
 * `ConfirmDialogService`, jeden wspólny dla całej apki, więc nie potrzebują własnego
 * stanu w tym komponencie (patrz `deleteBudget`/`disableBudget`).
 */
type Dialog = 'edit' | 'reset' | 'savingsLink' | null;

/** Pozycje menu wiersza. Ten sam zestaw kluczy co w `settings.budgets.actions.*`. */
type MenuAction = 'edit' | 'disable' | 'enable' | 'reset' | 'delete' | 'restore' | 'savingsLink';

/**
 * Stan panelu wyszukiwarki tekstowej (`nzCustomFilter`, kolumna „Nazwa").
 *
 * Wersja robocza (`draft`, edytowana w polu) i zastosowana (prywatna, użyta do
 * filtrowania) są rozdzielone — filtr działa dopiero po kliknięciu „Szukaj", nie przy
 * każdym naciśnięciu klawisza. To wzorzec z oficjalnego przykładu NG-ZORRO dla custom
 * filter panel (`nz-filter-trigger` + własny `nz-dropdown-menu`), nie improwizacja.
 */
class TextFilterState {
  readonly visible = signal(false);
  readonly draft = signal('');
  private readonly applied = signal('');
  readonly active = computed(() => this.applied().length > 0);

  search(): void {
    this.applied.set(this.draft().trim());
    this.visible.set(false);
  }

  reset(): void {
    this.draft.set('');
    this.applied.set('');
  }

  /** Bez uwzględniania wielkości liter i polskich ogonków — ten sam `normalizeText`
   *  co wyszukiwarka akcji w nagłówku, żeby dwa pola tekstowe w apce nie zachowywały się inaczej. */
  matches(value: string): boolean {
    const query = normalizeText(this.applied());
    return query.length === 0 || normalizeText(value).includes(query);
  }
}

/**
 * Stan panelu „od–do" na datach — kolumna „Data utworzenia", JEDEN `nz-range-picker`
 * (nie dwa osobne pickery — tak ustalił user).
 */
class DateRangeFilterState {
  readonly visible = signal(false);
  readonly draftRange = signal<[Date, Date] | null>(null);
  private readonly range = signal<[Date, Date] | null>(null);
  readonly active = computed(() => this.range() !== null);

  search(): void {
    this.range.set(this.draftRange());
    this.visible.set(false);
  }

  reset(): void {
    this.draftRange.set(null);
    this.range.set(null);
  }

  /**
   * `iso` przychodzi z API jako `DateTimeOffset` — porównujemy po DNIU w czasie lokalnym,
   * tym samym, który pokazuje kolumna (`day()` niżej), a nie po pełnym znaczniku czasu.
   * Inaczej budżet utworzony dziś o 23:50 wypadłby z filtra „do dzisiaj" ustawionego
   * na sam dzień dzisiejszy, bo w UTC to już byłby następny dzień.
   */
  matches(iso: string): boolean {
    const applied = this.range();
    if (applied === null) return true;

    const value = startOfDay(new Date(iso));
    const [from, to] = applied;
    return value >= startOfDay(from) && value <= startOfDay(to);
  }
}

function startOfDay(date: Date): number {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
}

@Component({
  selector: 'app-settings',
  imports: [
    FormsModule,
    NzAlertModule, NzBadgeModule, NzButtonModule, NzDatePickerModule, NzDropdownModule,
    NzEmptyModule, NzIconModule, NzInputModule, NzInputNumberModule, NzModalModule, NzSelectModule,
    NzSpinModule, NzTableModule, NzTabsModule, TranslatePipe, NzBreadCrumbComponent, NzBreadCrumbItemComponent,
    RangeFilter, PageHeader, Rules, Training, AccountSecurity,
  ],
  templateUrl: './settings.html',
  styleUrl: './settings.scss',
})
export class Settings {
  /** Parser polskiego formatu kwot dla pól `nz-input-number` — uzasadnienie przy `parseAmount`. */
  protected readonly parseAmount = parseAmount;

  private readonly route = inject(ActivatedRoute);
  private readonly activeBudget = inject(ActiveBudget);
  private readonly router = inject(Router);

  /**
   * Zakładka, z którą ekran się otwiera. Czytana z adresu RAZ, przy tworzeniu komponentu —
   * to wartość początkowa dla `nz-tabs`, nie wiązanie dwustronne.
   *
   * Bez `?tab=` w adresie nie dałoby się wrócić na „Dane treningowe": breadcrumb z listy
   * transakcji („Ustawienia / Dane treningowe / Lista transakcji") prowadziłby na ekran
   * otwarty na budżetach.
   */
  protected readonly initialTab =
    TabIndexByParam[this.route.snapshot.queryParamMap.get('tab') ?? ''] ?? 0;

  /** Która zakładka jest otwarta TERAZ — steruje leniwym montowaniem „Danych treningowych". */
  protected readonly activeTab = signal(this.initialTab);

  protected onTabChange(index: number): void {
    this.activeTab.set(index);

    // `replaceUrl`, bo przełączenie zakładki nie jest krokiem nawigacji: cofnięcie ma wrócić
    // tam, skąd użytkownik przyszedł na ustawienia, a nie przewijać jego kliknięcia w zakładki.
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab: TabParamByIndex[index] ?? null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  private readonly http = inject(HttpClient);
  private readonly translate = inject(TranslateService);
  private readonly message = inject(NzMessageService);
  private readonly errorMessages = inject(ErrorMessages);
  private readonly confirmDialog = inject(ConfirmDialogService);

  private readonly resource = httpResource<BudgetListResponse>(() => '/api/budgets');

  /** Bezpieczny odczyt — `value()` RZUCA w stanie bledu (patrz core/api/resource-value.ts). */
  private readonly resourceValue = valueOf(this.resource);

  protected readonly loading = this.resource.isLoading;
  protected readonly budgets = computed(() => this.resourceValue()?.budgets ?? []);

  /** Okno przywracania — liczba jedzie z API, nie z tekstu tłumaczenia (patrz model). */
  protected readonly retentionDays = computed(() => this.resourceValue()?.retentionDays ?? 30);

  /** Komunikat błędu operacji; `null` = wszystko w porządku. */
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  // ── Zaznaczanie wierszy ──────────────────────────────────────────────────────────────

  /**
   * Kolumna z checkboxem jest w makiecie, ale nie ma tam ŻADNEJ akcji masowej.
   * Zostawiamy samo zaznaczanie: dokładanie „usuń zaznaczone" na zapas byłoby wymyślaniem
   * operacji, której nikt nie zaprojektował — a masowe kasowanie budżetów to nie drobiazg.
   */
  protected readonly selected = signal<ReadonlySet<string>>(new Set());

  // Zaznaczanie liczy się względem tego, co WIDAĆ (po filtrach), nie względem całej listy —
  // inaczej „zaznacz wszystkie" przy aktywnym filtrze po cichu zaznaczałoby też wiersze,
  // których użytkownik nie widzi na ekranie.
  protected readonly allSelected = computed(() => {
    const rows = this.filteredBudgets();
    return rows.length > 0 && rows.every((b) => this.selected().has(b.id));
  });

  protected toggleRow(id: string, checked: boolean): void {
    const next = new Set(this.selected());
    if (checked) next.add(id); else next.delete(id);
    this.selected.set(next);
  }

  protected toggleAll(checked: boolean): void {
    this.selected.set(checked ? new Set(this.filteredBudgets().map((b) => b.id)) : new Set());
  }

  // ── Sortowanie i filtrowanie kolumn ──────────────────────────────────────────────────

  /**
   * Każda kolumna poza zaznaczaniem i akcjami jest SORTOWALNA i FILTROWALNA — inaczej niż
   * przy imporcie, gdzie ustalono „filtrowalne, nie sortowalne" (tam decydowała skala:
   * setki wierszy z wyciągu). Filtr dopasowuje się do rodzaju danych, nie jest jeden
   * wzorzec na wszystko:
   * - Nazwa: wyszukiwarka tekstowa (`TextFilterState`).
   * - Kwoty i liczba transakcji: zakres „od–do" (`RangeFilterState`) — to wartości ciągłe,
   *   więc pytanie brzmi „ile" nie „która dokładnie".
   * - Data utworzenia: zakres „od–do" na `nz-range-picker` (`DateRangeFilterState`).
   * - Status: zostaje przy liście checkboxów (`nzFilters`/`nzFilterFn`, wbudowane w nz-table).
   *   Ma z góry znany, stały zbiór trzech wartości — nie ma tu nic do wyszukiwania ani
   *   zakresu, więc nie ma powodu zamieniać gotowego mechanizmu na własny panel.
   *
   * Pierwsze pięć filtrów NIE korzysta z wbudowanego mechanizmu nz-table (`nzFilters`) —
   * przy custom panelu (`nzCustomFilter`) nz-table nie wie, jak filtrować samo; to komponent
   * filtruje `budgets()` i podaje wynik jako `[nzData]`. Sortowanie i filtr statusu działają
   * DALEJ na tym przefiltrowanym zbiorze — nz-table stosuje je już po naszej stronie.
   */
  protected readonly nameFilter = new TextFilterState();
  protected readonly balanceFilter = new RangeFilterState();
  protected readonly initialBalanceFilter = new RangeFilterState();
  protected readonly monthlyLimitFilter = new RangeFilterState();
  protected readonly createdAtFilter = new DateRangeFilterState();
  protected readonly transactionCountFilter = new RangeFilterState();

  protected readonly filteredBudgets = computed(() => this.budgets().filter((row) =>
    this.nameFilter.matches(row.name)
    && this.balanceFilter.matches(row.balance)
    && this.initialBalanceFilter.matches(row.initialBalance)
    && this.monthlyLimitFilter.matches(row.monthlyLimit)
    && this.createdAtFilter.matches(row.createdAt)
    && this.transactionCountFilter.matches(row.transactionCount)));

  protected readonly sortByName = (a: BudgetListItem, b: BudgetListItem): number =>
    a.name.localeCompare(b.name, 'pl');

  protected readonly sortByBalance = (a: BudgetListItem, b: BudgetListItem): number =>
    a.balance - b.balance;

  protected readonly sortByInitialBalance = (a: BudgetListItem, b: BudgetListItem): number =>
    a.initialBalance - b.initialBalance;

  protected readonly sortByMonthlyLimit = (a: BudgetListItem, b: BudgetListItem): number =>
    a.monthlyLimit - b.monthlyLimit;

  // Porównanie po ISO string działa wprost — format ma stałą szerokość pól, więc sortuje
  // się tak samo jak po dacie, bez parsowania.
  protected readonly sortByCreatedAt = (a: BudgetListItem, b: BudgetListItem): number =>
    a.createdAt.localeCompare(b.createdAt);

  protected readonly sortByTransactionCount = (a: BudgetListItem, b: BudgetListItem): number =>
    a.transactionCount - b.transactionCount;

  protected readonly statusFilters = computed(() =>
    (['active', 'disabled', 'deleted'] as BudgetStatus[]).map((value) => ({
      text: this.translate.instant(`settings.budgets.status.${value}`),
      value,
    })),
  );

  protected readonly filterByStatus = (selected: BudgetStatus[], row: BudgetListItem): boolean =>
    selected.length === 0 || selected.includes(row.status);

  /** Aktywny → Wyłączony → Usunięty — logiczna kolejność „stanu sprawy", nie alfabetyczna. */
  private static readonly STATUS_ORDER: Record<BudgetStatus, number> =
    { active: 0, disabled: 1, deleted: 2 };

  protected readonly sortByStatus = (a: BudgetListItem, b: BudgetListItem): number =>
    Settings.STATUS_ORDER[a.status] - Settings.STATUS_ORDER[b.status];

  // ── Menu wiersza ─────────────────────────────────────────────────────────────────────

  /** Wiersz, nad którego menu stoi kursor. Ustawiany przy otwarciu `⋮`. */
  protected readonly menuRow = signal<BudgetListItem | null>(null);

  /**
   * Które pozycje ma menu tego wiersza — DANE, nie łańcuch `@if` w szablonie.
   *
   * Reguła „usunięty budżet ma tylko przywracanie" i zamiana „Wyłącz" ↔ „Włącz" to
   * zachowanie, które musi dać się sprawdzić testem. Rozpisane po szablonie byłoby
   * sprawdzalne wyłącznie przez otwarty overlay dropdownu.
   */
  protected readonly menuItems = computed<{ action: MenuAction; danger: boolean }[]>(() => {
    const row = this.menuRow();
    if (!row) return [];

    // Usunięty ma jedną sensowną akcję — reszta dotyczy budżetu, który wciąż istnieje.
    if (row.status === 'deleted') return [{ action: 'restore', danger: false }];

    return [
      { action: 'edit', danger: false },
      { action: 'savingsLink', danger: false },
      { action: row.status === 'disabled' ? 'enable' : 'disable', danger: false },
      { action: 'reset', danger: false },
      { action: 'delete', danger: true },
    ];
  });

  protected runMenu(action: MenuAction): void {
    switch (action) {
      case 'edit': this.openEdit(); break;
      case 'savingsLink': this.openSavingsLink(); break;
      case 'enable': void this.setEnabled(true); break;
      case 'disable': void this.disableBudget(); break;
      case 'reset': this.openReset(); break;
      case 'delete': void this.deleteBudget(); break;
      case 'restore': void this.restore(); break;
    }
  }

  protected readonly dialog = signal<Dialog>(null);

  /** Wiersz, którego dotyczy otwarty modal — zamrożony w chwili otwarcia. */
  protected readonly target = signal<BudgetListItem | null>(null);

  // ── Modal edycji ─────────────────────────────────────────────────────────────────────

  protected readonly draftName = signal('');
  protected readonly draftInitialBalance = signal(0);

  /**
   * Alert „to przeliczy cały budżet" pokazujemy dopiero, gdy bilans początkowy FAKTYCZNIE
   * się zmienił. Ostrzeżenie widoczne od otwarcia modala nauczyłoby użytkownika je ignorować,
   * a to jedyne miejsce, w którym jedna liczba przesuwa cały wykres bilansu.
   */
  protected readonly initialBalanceChanged = computed(() =>
    this.target() !== null && this.draftInitialBalance() !== this.target()!.initialBalance,
  );

  /** Bilans bieżący w modalu jest tylko do odczytu — liczymy go, więc nie da się go „ustawić". */
  protected readonly previewBalance = computed(() => {
    const row = this.target();
    if (!row) return 0;
    return this.draftInitialBalance() + (row.balance - row.initialBalance);
  });

  protected readonly canSaveEdit = computed(() => this.draftName().trim().length > 0);

  protected openEdit(): void {
    const row = this.menuRow();
    if (!row) return;

    this.draftName.set(row.name);
    this.draftInitialBalance.set(row.initialBalance);
    this.target.set(row);
    this.dialog.set('edit');
  }

  // ── Modal resetu ─────────────────────────────────────────────────────────────────────

  protected openReset(): void {
    const row = this.menuRow();
    if (!row) return;

    this.target.set(row);
    this.dialog.set('reset');
  }

  protected closeDialog(): void {
    this.dialog.set(null);
    this.target.set(null);
  }

  // ── Modal reguł powiązania z budżetem oszczędnościowym (#10) ────────────────────────
  //
  // Bez makiety — propozycja Figma (node-id=242-3060), zanim ekran dostanie właściwą makietę.

  protected readonly draftLinkedBudgetId = signal<string | null>(null);
  protected readonly draftRules = signal<TitleAmountRule[]>([]);

  /** Budżety, które można wskazać jako powiązane — bez samego siebie i bez usuniętych. */
  protected readonly savingsLinkOptions = computed(() => {
    const row = this.target();
    return this.budgets().filter((b) => b.status !== 'deleted' && b.id !== row?.id);
  });

  /**
   * Powiązanie bez ani jednej reguły nigdy niczego by nie wykluczyło z sum — stąd wymóg
   * co najmniej jednej WYPEŁNIONEJ reguły, gdy budżet jest wskazany. Zdjęcie powiązania
   * (budżet = null) nie wymaga niczego — reguły idą wtedy w komplet.
   */
  protected readonly canSaveSavingsLink = computed(() => {
    if (this.draftLinkedBudgetId() === null) return true;
    return this.draftRules().some((r) => r.titlePattern.trim().length >= 3 && r.amountTo > 0);
  });

  protected openSavingsLink(): void {
    const row = this.menuRow();
    if (!row) return;

    this.draftLinkedBudgetId.set(row.linkedSavingsBudgetId);
    this.draftRules.set(
      row.savingsTransferRules.length > 0
        ? row.savingsTransferRules.map((r) => ({ ...r }))
        : [{ titlePattern: '', amountFrom: 0, amountTo: 0 }],
    );
    this.target.set(row);
    this.dialog.set('savingsLink');
  }

  protected addSavingsRule(): void {
    this.draftRules.set([...this.draftRules(), { titlePattern: '', amountFrom: 0, amountTo: 0 }]);
  }

  protected removeSavingsRule(index: number): void {
    this.draftRules.set(this.draftRules().filter((_, i) => i !== index));
  }

  protected updateSavingsRule(index: number, patch: Partial<TitleAmountRule>): void {
    this.draftRules.set(this.draftRules().map((r, i) => (i === index ? { ...r, ...patch } : r)));
  }

  protected async saveSavingsLink(): Promise<void> {
    const row = this.target();
    if (!row || !this.canSaveSavingsLink()) return;

    const linkedSavingsBudgetId = this.draftLinkedBudgetId();
    const rules = linkedSavingsBudgetId === null
      ? []
      : this.draftRules().filter((r) => r.titlePattern.trim().length > 0);

    await this.run(
      () => firstValueFrom(this.http.put(`/api/budgets/${row.id}/savings-link`, {
        linkedSavingsBudgetId,
        rules,
      })),
      'settings.budgets.savingsLink.toast.updated',
    );
  }

  // ── Operacje ─────────────────────────────────────────────────────────────────────────

  protected async saveEdit(): Promise<void> {
    const row = this.target();
    if (!row || !this.canSaveEdit()) return;

    await this.run(
      () => firstValueFrom(this.http.put<BudgetListItem>(`/api/budgets/${row.id}`, {
        name: this.draftName().trim(),
        initialBalance: this.draftInitialBalance(),
      })),
      'settings.budgets.toast.updated',
    );
  }

  /**
   * Włączenie jest odwracalne bez śladu (wyłączenie z powrotem to jeden klik), więc
   * zostaje instant — pytanie o potwierdzenie byłoby tarciem bez powodu. Wyłączenie
   * idzie przez `ConfirmDialogService`: dalej daje się edytować, resetować i usuwać,
   * ale odcina import i dopisywanie transakcji, więc user powinien to świadomie kliknąć.
   */
  protected async setEnabled(enabled: boolean): Promise<void> {
    const row = this.menuRow();
    if (!row) return;

    await this.run(
      () => firstValueFrom(this.http.post<BudgetListItem>(
        `/api/budgets/${row.id}/${enabled ? 'enable' : 'disable'}`, {})),
      enabled ? 'settings.budgets.toast.enabled' : 'settings.budgets.toast.disabled',
    );
  }

  protected async disableBudget(): Promise<void> {
    const row = this.menuRow();
    if (!row) return;

    const confirmed = await this.confirmDialog.confirm({
      header: this.translate.instant('settings.budgets.disableConfirm.header', { name: row.name }),
      description: this.translate.instant('settings.budgets.disableConfirm.description'),
      confirmText: this.translate.instant('settings.budgets.actions.disable'),
    });
    if (!confirmed) return;

    await this.setEnabled(false);
  }

  protected async confirmReset(): Promise<void> {
    const row = this.target();
    if (!row) return;

    await this.run(
      () => firstValueFrom(this.http.post<BudgetListItem>(`/api/budgets/${row.id}/reset`, {})),
      'settings.budgets.toast.reset',
    );
  }

  /**
   * Usunięcie potwierdza się WPISANIEM NAZWY (makieta), nie samym klikiem — zabiera ze sobą
   * wszystkie transakcje budżetu, a nazwa jest jedyną rzeczą, która odróżnia dwa budżety
   * tego samego miesiąca. `ConfirmDialogService` sam pilnuje, żeby przycisk był zablokowany,
   * dopóki wpisany tekst się nie zgadza — tu tylko podajemy `confirmKey`.
   */
  protected async deleteBudget(): Promise<void> {
    const row = this.menuRow();
    if (!row) return;

    const confirmed = await this.confirmDialog.confirm({
      header: this.translate.instant('settings.budgets.delete.header', { name: row.name }),
      description: this.translate.instant(
        'settings.budgets.delete.description', { days: this.retentionDays() }),
      confirmKey: row.name,
      confirmText: this.translate.instant('settings.budgets.delete.confirm'),
    });
    if (!confirmed) return;

    await this.run(
      // `forget` dopiero po udanym usunięciu — patrz `ActiveBudget.forget`.
      () => firstValueFrom(this.http.delete(`/api/budgets/${row.id}`)).then(() => this.activeBudget.forget(row.id)),
      'settings.budgets.toast.deleted',
    );
  }

  protected async restore(): Promise<void> {
    const row = this.menuRow();
    if (!row) return;

    await this.run(
      () => firstValueFrom(this.http.post<BudgetListItem>(`/api/budgets/${row.id}/restore`, {})),
      'settings.budgets.toast.restored',
    );
  }

  /**
   * Wspólny przebieg każdej operacji: zablokuj, wywołaj, odśwież listę, pokaż komunikat.
   *
   * Lista odświeża się z SERWERA, a nie przez podmianę wiersza w pamięci: reset i usunięcie
   * zmieniają liczbę transakcji i bilans, a przywrócenie potrafi cofnąć kilka wierszy naraz.
   * Ręczne sklejanie tego stanu po stronie frontu rozjechałoby się przy pierwszej zmianie
   * w handlerze.
   */
  private async run(operation: () => Promise<unknown>, toastKey: string): Promise<void> {
    this.busy.set(true);
    this.error.set(null);

    try {
      await operation();
      this.closeDialog();
      this.selected.set(new Set());
      this.resource.reload();
      this.message.info(this.translate.instant(toastKey));
    } catch (e) {
      this.error.set(this.errorMessages.of(e, 'settings.budgets.error'));
    } finally {
      this.busy.set(false);
    }
  }

  /** Komunikat z API, gdy jest; inaczej własny tekst o awarii sieci. */

  // ── Formatowanie ─────────────────────────────────────────────────────────────────────

  /**
   * `useGrouping: true`, nie domyślne `'auto'`: w `pl` tryb automatyczny NIE rozdziela
   * liczb czterocyfrowych, więc 2099,86 wyglądałoby inaczej niż 12 099,86.
   */
  protected money(value: number): string {
    return new Intl.NumberFormat('pl-PL', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
      useGrouping: true,
    }).format(value);
  }

  protected day(iso: string): string {
    return new Date(iso).toLocaleDateString('pl-PL', {
      day: '2-digit', month: '2-digit', year: 'numeric',
    });
  }
}
