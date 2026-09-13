import { Component, computed, effect, inject, linkedSignal, signal } from '@angular/core';
import { ActiveBudget } from '../../core/active-budget';
import { CommonModule } from '@angular/common';
import { HttpClient, httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { NgApexchartsModule } from 'ng-apexcharts';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmDialogService } from '../../core/confirm-dialog/confirm-dialog.service';
import { ErrorMessages } from '../../core/errors/error-messages';
import { errorOf, valueOf } from '../../core/api/resource-value';
import { CATEGORY_SERIES_COLORS, CHART_COLORS } from '../../core/chart-palette';
import { SavingsMonth, SavingsResponse } from '../../core/api/models/savings';
import { ReservationsResponse, SavingsReservation } from '../../core/api/models/reservations';
import { parseAmount } from '../../core/parse-amount';
import { BudgetSwitcher } from '../../core/budget-switcher/budget-switcher';

/**
 * Miesiące w MIEJSCOWNIKU — używane wyłącznie w zdaniach „w …".
 *
 * `Intl` zna tylko mianownik, a „w lipiec 2026" to nie jest polszczyzna. Lista jest krótka,
 * zamknięta i nigdy się nie zmieni, więc tabela jest tu tańsza i czytelniejsza niż biblioteka.
 */
const MONTHS_LOCATIVE_PL = [
  'styczniu', 'lutym', 'marcu', 'kwietniu', 'maju', 'czerwcu',
  'lipcu', 'sierpniu', 'wrześniu', 'październiku', 'listopadzie', 'grudniu',
];

/**
 * Stan ekranu — to on decyduje, co mówi baner u góry.
 *
 * Kolejność rozstrzygania jest REGUŁĄ, nie kosmetyką: dwa pierwsze stany są nadrzędne, bo
 * opisują brak warunków do postawienia jakiejkolwiek tezy. Ekran, który przy braku celu
 * pokazuje „cel nieosiągnięty", oskarża użytkownika o niedotrzymanie czegoś, czego nigdy
 * nie zadeklarował.
 */
type ScreenState = 'noGoal' | 'noLargeExpense' | 'proof' | 'missed' | 'plain';

@Component({
  selector: 'app-savings',
  imports: [
    CommonModule, FormsModule, RouterLink, NgApexchartsModule, BudgetSwitcher,
    NzAlertModule, NzBreadCrumbModule, NzButtonModule, NzEmptyModule, NzIconModule,
    NzInputNumberModule, NzModalModule, NzSpinModule, NzStatisticModule,
    NzTableModule, NzTagModule,
    TranslatePipe,
  ],
  templateUrl: './savings.html',
  styleUrl: './savings.scss',
})
export class Savings {
  /** Parser polskiego formatu kwot dla pól `nz-input-number` — uzasadnienie przy `parseAmount`. */
  protected readonly parseAmount = parseAmount;

  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly message = inject(NzMessageService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly errorMessages = inject(ErrorMessages);

  /** Patrz App.langLoaded — bez tego serie wykresu zostałyby na kluczach tłumaczeń. */
  private readonly langLoaded = toSignal(this.translate.onLangChange, { initialValue: null });

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  private readonly activeBudget = inject(ActiveBudget);

  /** Budżety z adresu — ta sama nazwa parametru co na liście transakcji. */
  private readonly budgetIdsFromUrl = computed(() => this.queryParams().getAll('budgetId'));

  /**
   * Budżety, na których liczy ten ekran: z adresu, a gdy adres milczy — ten, który użytkownik ma
   * w widoku (`ActiveBudget`). Bez tego każde wejście bez `budgetId` (nagłówek, okruszki) pokazywało
   * budżet domyślny zamiast wybranego — issue #16.
   */
  protected readonly budgetIds = computed(() => [...this.activeBudget.resolve(this.budgetIdsFromUrl())]);

  /** Wejście z `?budgetId` (np. z listy transakcji) ustawia budżet w widoku dla kolejnych ekranów. */
  private readonly publishBudgetFromUrl = effect(() => {
    const fromUrl = this.budgetIdsFromUrl();
    if (fromUrl.length > 0) this.activeBudget.set(fromUrl);
  });

  /** Wybór z przełącznika idzie do ADRESU — patrz `BudgetSwitcher`, dlaczego nie wprost do `ActiveBudget`. */
  protected switchBudget(id: string): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { budgetId: id },
      queryParamsHandling: 'merge',
    });
  }

  private readonly resource = httpResource<SavingsResponse>(() => ({
    url: '/api/savings',
    // Rozsypanie przez spread, nie `? {…} : {}` — pusty obiekt z opcjonalnym kluczem nie wpasowuje
    // się w typ parametrów HttpClient. Ta sama forma co na liście transakcji.
    params: { ...(this.budgetIds().length > 0 ? { budgetId: this.budgetIds() } : {}) },
  }));

  /** `value()` RZUCA w stanie błędu — patrz core/api/resource-value.ts. */
  private readonly value = valueOf(this.resource);

  protected readonly loading = this.resource.isLoading;
  protected readonly failure = errorOf(this.resource);
  protected readonly busy = signal(false);

  protected readonly data = computed(() => this.value() ?? null);

  /**
   * Ostatnia odpowiedź, która przyszła — WYŁĄCZNIE dla przełącznika budżetu.
   *
   * Po wyborze innego budżetu zasób ładuje od nowa i `data()` na chwilę jest puste. Bez tej pamięci
   * przełącznik znikałby dokładnie w chwili, w której się go użyło, a układ podskakiwał.
   */
  protected readonly lastData = linkedSignal<SavingsResponse | null, SavingsResponse | null>({
    source: this.data,
    computation: (next, prev) => next ?? prev?.value ?? null,
  });

  protected readonly months = computed(() => this.data()?.months ?? []);
  protected readonly goal = computed(() => this.data()?.goal ?? null);

  /**
   * Miesiąc, o którym mówi baner-dowód: NAJNOWSZY z werdyktem „dowód".
   *
   * Dowód jest faktem trwałym — raz postawiony, zostaje. Dlatego bierzemy najnowszy, a nie
   * „bieżący, jeśli akurat jest dowodem": inaczej zdanie znikałoby z ekranu pierwszego dnia
   * nowego miesiąca, choć nic się nie zmieniło.
   */
  protected readonly latestProof = computed(
    () => this.months().find((m) => m.verdict === 'Proof') ?? null,
  );

  /**
   * Bieżący miesiąc — i tylko on zasila kafel postępu.
   *
   * ⚠️ Nie mieszać z <see cref="latestProof" />: postęp opisuje TERAZ, dowód opisuje przeszłość.
   */
  protected readonly current = computed(() => this.data()?.currentMonth ?? null);

  /**
   * Ostatni ZAMKNIĘTY miesiąc z niedoborem — materiał na baner „cel nieosiągnięty".
   *
   * ⚠️ Zamknięty, nie bieżący. Zdanie „zabrakło 600 zł do celu" postawione 5. dnia miesiąca
   * jest po prostu nieprawdziwe — miesiąc jeszcze trwa i nic jeszcze nie zabrakło.
   * Postęp bieżącego miesiąca pokazuje kafel, i to jest właściwe miejsce.
   */
  protected readonly lastMissed = computed(() => {
    const currentMonth = this.current()?.month;
    return this.months().find((m) => m.month !== currentMonth && m.verdict === 'GoalMissed') ?? null;
  });

  protected readonly state = computed<ScreenState>(() => {
    const data = this.data();
    if (data === null) return 'plain';
    if (data.goal === null) return 'noGoal';

    // Bez ani jednej oznaczonej transakcji dowód nie ma jak powstać — i trzeba to powiedzieć
    // wprost, zamiast pokazywać „0 dowodów" jak fakt o dyscyplinie użytkownika.
    if (!data.hasAnyLargeExpense) return 'noLargeExpense';

    if (this.latestProof() !== null) return 'proof';
    return this.lastMissed() !== null ? 'missed' : 'plain';
  });

  /**
   * Czy ostrzec, że wpłat nie widać. Dotyczy TYLKO stanu z ustawionym celem — bez celu
   * nie ma czego mierzyć, więc zdanie o kategorii byłoby szumem.
   *
   * Powód istnienia: przed #10 odkładanie rozpoznaje kategoria „Oszczędności", więc przelew
   * opisany inaczej nie zostanie policzony. Pokazanie zera bez tego zdania byłoby stwierdzeniem
   * faktu, którego nikt nie sprawdził.
   */
  protected readonly warnNoSavingsSeen = computed(
    () => this.data() !== null && this.goal() !== null && !this.data()!.hasAnySavings,
  );

  // ── Kafel rezerwacji (makieta 147:96) ────────────────────────────────────────────────

  /**
   * Rezerwacje jadą OSOBNYM żądaniem, a nie doklejką do `/api/savings`.
   *
   * ⚠️ To dwie różne wielkości: tamten endpoint liczy DYSCYPLINĘ (ile odkładasz co miesiąc),
   * ten STAN (co z odłożonego jest wolne). Zlanie ich w jedną odpowiedź zmusiłoby ekran listy
   * rezerwacji do pobierania całej historii miesięcy, której w ogóle nie pokazuje.
   */
  private readonly reservationsResource = httpResource<ReservationsResponse>(() => ({
    url: '/api/savings/reservations',
    params: { ...(this.budgetIds().length > 0 ? { budgetId: this.budgetIds() } : {}) },
  }));

  private readonly reservationsValue = valueOf(this.reservationsResource);

  protected readonly reservations = computed(() => this.reservationsValue() ?? null);
  protected readonly reservationRows = computed<SavingsReservation[]>(
    () => this.reservations()?.reservations ?? [],
  );

  /**
   * Nadmiar rezerwacji ponad stan konta — osobny stan, nie minus w kaflu.
   *
   * Ujemne „wolne środki" to poprawna informacja i nie wolno jej ukryć, ale kafel z minusem
   * czyta się jak błąd aplikacji, a nie jak stan finansów.
   */
  protected readonly overReserved = computed(() => {
    const free = this.reservations()?.freeFunds;
    return free !== undefined && free < 0 ? -free : null;
  });

  /** Ile procent rezerwacji jest pokryte pieniędzmi — wypełnienie pierścienia. */
  protected reservationPercent(row: SavingsReservation): number {
    return row.amount <= 0 ? 0 : Math.round((row.collected / row.amount) * 100);
  }

  /**
   * Koncentryczne pierścienie — JEDEN na rezerwację, dokładnie jak na makiecie (`147:96`).
   *
   * <para>
   * ⚠️ To musi być jeden wykres <c>radialBar</c>, a nie kilka osobnych kółek postępu obok siebie.
   * Różnica nie jest kosmetyczna: pierścienie ułożone współśrodkowo pokazują rezerwacje jako
   * JEDNĄ pulę dzieloną na części, a rząd oddzielnych kółek — jako niezależne, niepowiązane
   * paski. Ta funkcja mówi właśnie o dzieleniu jednej puli, więc kształt wykresu jest tu treścią.
   * </para>
   *
   * Etykieta w środku (<c>total</c>) niesie sumę, a nie średnią z serii — Apex domyślnie
   * uśrednia procenty, co dałoby liczbę bez żadnego znaczenia.
   */
  protected readonly reservationChart = computed(() => {
    this.langLoaded();

    const rows = this.reservationRows();
    const summary = this.reservations();

    return {
      series: rows.map((r) => this.reservationPercent(r)),
      labels: rows.map((r) => r.name),
      colors: rows.map(
        (_, i) => CATEGORY_SERIES_COLORS[i % CATEGORY_SERIES_COLORS.length],
      ),
      chart: { type: 'radialBar' as const, height: 340, fontFamily: 'inherit' },
      plotOptions: {
        radialBar: {
          hollow: { size: '40%' },
          track: { background: CHART_COLORS.neutral200, margin: 6 },
          dataLabels: {
            name: { fontSize: '13px', color: CHART_COLORS.neutral500 },
            value: {
              fontSize: '16px',
              color: CHART_COLORS.neutral900,
              formatter: (value: number) => `${Math.round(value)}%`,
            },
            total: {
              show: true,
              label: this.translate.instant('reservations.collected'),
              color: CHART_COLORS.primary500,
              // ⚠️ Własny formatter, bo domyślny liczy ŚREDNIĄ z procentów serii. Środek ma
              // pokazywać kwoty („1 800 z 5 000 zł"), a nie uśredniony procent czterech kopert.
              formatter: () => `${this.wholeMoney(summary?.collectedTotal)} z ${this.wholeMoney(summary?.reservedTotal)} zł`,
            },
          },
        },
      },
      // Legenda NAD wykresem, wyrównana do lewej. Makieta trzyma ją z boku, ale kafel stoi
      // w wąskiej kolumnie — legenda z boku zabierała tam tyle szerokości, że pierścienie
      // wychodziły poza kartę. Treść legendy zostaje ta sama, zmienia się tylko miejsce.
      legend: {
        show: true,
        position: 'top' as const,
        horizontalAlign: 'left' as const,
        fontSize: '12px',
      },
      stroke: { lineCap: 'round' as const },
    };
  });

  // ── Wykres ───────────────────────────────────────────────────────────────────────────

  /**
   * „Ile odłożyłeś w miesiącu" — oś Y to KWOTA ODŁOŻONA, nie bilans konta.
   *
   * ⚠️ Próg celu jest SCHODKIEM, nie prostą, i to jest tu najważniejsze. Cel zmienia się
   * w czasie (zmiana kwoty kończy poprzedni datą i zakłada nowy), więc jedna pozioma linia
   * na dzisiejszej wartości pokazałaby starsze miesiące jako niezaliczone, choć wtedy cel był
   * niższy i WYSZEDŁ. Prosta linia zamieniłaby historię dowodów w historię porażek — a na
   * wykresie nikt by tego nie zauważył jako błędu. Stąd druga seria: cel per miesiąc, ze
   * `stepline`, prosto z odpowiedzi serwera (to on zna historię celów).
   */
  protected readonly chart = computed(() => {
    this.langLoaded();

    // Rosnąco — oś czasu ma biec w lewo→prawo, a `months` przychodzi od najnowszego.
    const points = [...this.months()].reverse();

    return {
      series: [
        {
          name: this.translate.instant('savings.chart.deposited'),
          type: 'column' as const,
          data: points.map((p) => p.deposited),
        },
        {
          name: this.translate.instant('savings.chart.goal'),
          type: 'line' as const,
          data: points.map((p) => p.goal),
        },
      ],
      chart: { type: 'line' as const, height: 320, fontFamily: 'inherit', toolbar: { show: false } },
      // Kolumny w kolorze podstawowym, próg neutralny — próg jest odniesieniem, nie danymi.
      colors: [CHART_COLORS.primary500, CHART_COLORS.neutral500],
      stroke: {
        // `stepline` na serii celu — to ona jest schodkiem. Kolumny nie mają obrysu.
        width: [0, 2],
        curve: ['smooth', 'stepline'] as ('smooth' | 'stepline')[],
        dashArray: [0, 4],
      },
      // Znacznik na miesiącach z jednorazowym wydatkiem — to one są testem obciążeniowym,
      // więc muszą być rozpoznawalne bez czytania tabeli.
      markers: {
        size: points.map((p) => (p.oneOffCount > 0 ? 6 : 0)),
        strokeWidth: 0,
      },
      xaxis: { categories: points.map((p) => this.monthLabel(p.month)) },
      yaxis: { labels: { formatter: (v: number) => this.money(v) } },
      legend: { show: true },
      dataLabels: { enabled: false },
    };
  });

  // ── Cel ──────────────────────────────────────────────────────────────────────────────

  protected readonly goalModalOpen = signal(false);
  protected readonly draftAmount = signal<number | null>(null);

  /**
   * Miesiąc, od którego ma obowiązywać PIERWSZY cel — pole widoczne tylko wtedy.
   *
   * ⚠️ Przy zmianie istniejącego celu serwer i tak je zignoruje (wsteczna data byłaby tam
   * furtką do wyprodukowania dowodu), więc pokazywanie kontrolki, która nic nie robi, byłoby
   * atrapą. Stąd pole znika, gdy cel już istnieje, a modal mówi wtedy, co się stanie.
   */
  protected readonly draftStartedOn = signal<string | null>(null);

  /** Najstarszy miesiąc z danymi — sensowna podpowiedź „od kiedy mierzyć". */
  protected readonly earliestMonth = computed(() => {
    const months = this.months();
    return months.length > 0 ? months[months.length - 1].month : null;
  });

  protected openGoalModal(): void {
    this.draftAmount.set(this.goal()?.amount ?? null);
    this.draftStartedOn.set(this.earliestMonth());
    this.goalModalOpen.set(true);
  }

  protected async saveGoal(): Promise<void> {
    const amount = this.draftAmount();
    if (amount === null || amount <= 0) return;

    this.busy.set(true);
    try {
      await firstValueFrom(this.http.post('/api/savings/goal', {
        amount,
        budgetId: this.budgetIds()[0] ?? null,
        // Wysyłane tylko przy pierwszym celu — przy zmianie serwer to pole ignoruje.
        startedOn: this.goal() === null ? this.draftStartedOn() : null,
      }));
      this.goalModalOpen.set(false);
      this.message.success(this.translate.instant('savings.goalSaved'));
      this.resource.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** Podniesienie celu do proponowanej kwoty — ta sama droga zapisu, bez własnego endpointu. */
  protected async raiseGoal(): Promise<void> {
    const suggestion = this.data()?.raiseSuggestion;
    if (!suggestion) return;

    this.draftAmount.set(suggestion.suggestedAmount);
    this.goalModalOpen.set(true);
  }

  protected async endGoal(): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      header: this.translate.instant('savings.endConfirm.header'),
      description: this.translate.instant('savings.endConfirm.description'),
    });
    if (!ok) return;

    this.busy.set(true);
    try {
      const budgetId = this.budgetIds()[0];
      await firstValueFrom(this.http.delete(
        `/api/savings/goal${budgetId ? `?budgetId=${budgetId}` : ''}`));
      this.message.success(this.translate.instant('savings.goalEnded'));
      this.resource.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  // ── Akcje na wierszu ─────────────────────────────────────────────────────────────────

  /**
   * „To nie był jednorazowy wydatek" — zdejmuje flagę ze WSZYSTKICH oznaczonych transakcji
   * tego miesiąca.
   *
   * ⚠️ Idzie istniejącym `bulk-large-expense`, a nie nowym endpointem. `IsLargeExpense` ma już
   * jedną drogę zapisu i dwie rozjechałyby się — a przy okazji tamta wymusza zasięg budżetu
   * po stronie serwera, więc nie trzeba go tu odtwarzać.
   */
  protected async clearOneOff(month: SavingsMonth): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      header: this.translate.instant('savings.clearOneOffConfirm.header'),
      description: this.translate.instant('savings.clearOneOffConfirm.description', {
        month: this.monthLabel(month.month),
        count: month.oneOffCount,
      }),
    });
    if (!ok) return;

    const from = month.month;
    const to = this.lastDayOf(month.month);

    this.busy.set(true);
    try {
      await firstValueFrom(this.http.post('/api/transactions/bulk-large-expense', {
        selection: {
          ids: null,
          filter: {
            budgetIds: this.budgetIds().length > 0 ? this.budgetIds() : null,
            from, to,
            categoryId: null,
            uncategorized: false,
            direction: 'Expense',
            status: null,
            amountFrom: null,
            amountTo: null,
            search: null,
          },
        },
        isLargeExpense: false,
      }));
      this.message.success(this.translate.instant('savings.oneOffCleared'));
      this.resource.reload();
    } catch (e) {
      this.message.error(this.errorMessages.of(e));
    } finally {
      this.busy.set(false);
    }
  }

  /** Przejście do listy transakcji zawężonej do tego miesiąca — „Pokaż w transakcjach". */
  protected showInTransactions(month: SavingsMonth): void {
    void this.router.navigate(['/transactions'], {
      queryParams: {
        from: month.month,
        to: this.lastDayOf(month.month),
        ...(this.budgetIds().length > 0 ? { budgetId: this.budgetIds() } : {}),
      },
    });
  }

  /**
   * Czy to wiersz miesiąca, który WŁAŚNIE TRWA.
   *
   * Używane wyłącznie do wyciszenia werdyktu „nieosiągnięty": porażki nie ma jeszcze czego
   * ogłaszać, skoro miesiąc się nie skończył. Dowód jest odwrotnie — raz spełniony, jest
   * prawdą od razu i nie ma powodu z nim czekać.
   */
  protected isCurrent(month: SavingsMonth): boolean {
    return month.month === this.current()?.month;
  }

  protected reload(): void {
    this.resource.reload();
    this.reservationsResource.reload();
  }

  protected errorText(error: unknown): string {
    return this.errorMessages.of(error);
  }

  // ── Formatowanie ─────────────────────────────────────────────────────────────────────

  protected money(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return new Intl.NumberFormat('pl-PL', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
      .format(value);
  }

  /**
   * Kwota w pełnych złotych — WYŁĄCZNIE do etykiet wykresu.
   *
   * Groszy nie ma tu z dwóch powodów: tak jest na makiecie („1 800 z 5 000 zł") i tak musi być,
   * żeby napis zmieścił się w środku pierścienia. Reszta ekranu dalej używa <c>money()</c>
   * z dwoma miejscami — to są liczby do czytania co do grosza, a nie podpis pod grafiką.
   */
  protected wholeMoney(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return new Intl.NumberFormat('pl-PL', { maximumFractionDigits: 0 }).format(value);
  }

  protected monthLabel(iso: string): string {
    const [year, month] = iso.split('-');
    return new Intl.DateTimeFormat('pl-PL', { month: 'long', year: 'numeric' })
      .format(new Date(Number(year), Number(month) - 1, 1));
  }

  /**
   * Miesiąc w miejscowniku — „w lipcu 2026", nie „w lipiec 2026".
   *
   * ⚠️ `Intl` zna tylko mianownik, a to zdanie („odłożyłeś… także w LIPCU") jest najważniejszym
   * na całym ekranie. Zdanie-dowód napisane łamaną polszczyzną podważa samo siebie: brzmi jak
   * automat, a ma brzmieć jak stwierdzenie faktu.
   *
   * Odmiana jest w kodzie, nie w tłumaczeniach, bo to własność JĘZYKA, a nie treści — angielski
   * przypadków nie ma i tam ta sama funkcja zwróci po prostu nazwę.
   */
  protected monthLabelIn(iso: string): string {
    const [year, month] = iso.split('-').map(Number);
    const locative = MONTHS_LOCATIVE_PL[month - 1];
    return locative ? `${locative} ${year}` : this.monthLabel(iso);
  }

  /** Ostatni dzień miesiąca — dzień 0 następnego miesiąca, bez tabeli długości miesięcy. */
  private lastDayOf(iso: string): string {
    const [year, month] = iso.split('-').map(Number);
    const last = new Date(year, month, 0);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${last.getFullYear()}-${pad(last.getMonth() + 1)}-${pad(last.getDate())}`;
  }
}
