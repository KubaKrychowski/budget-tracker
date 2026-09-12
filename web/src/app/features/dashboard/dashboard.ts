import { Component, HostListener, computed, effect, inject, signal, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { httpResource } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ChartComponent, NgApexchartsModule } from 'ng-apexcharts';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzBreadCrumbModule } from 'ng-zorro-antd/breadcrumb';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { DashboardResponse } from '../../core/api/models/dashboard-response';
import { fromIsoDate, toIsoDate } from '../../core/api/date-param';
import { QUICK_ACTIONS, actionByKey } from '../../core/system-actions';
import { SystemAction } from '../../core/models/system-action';
import { CATEGORY_SERIES_COLORS, CHART_COLORS } from '../../core/chart-palette';
import { loadRememberedRange, rememberRange } from './budget-range-memory';
import { snapToAvailableRange } from './chart-range-selection';
import { valueOf } from '../../core/api/resource-value';

@Component({
  selector: 'app-dashboard',
  imports: [
    CommonModule, FormsModule, NgApexchartsModule,
    NzBreadCrumbModule, NzButtonModule, NzDatePickerModule, NzEmptyModule,
    NzIconModule, NzSelectModule, NzSpinModule, NzStatisticModule,
    NzAlertModule, NzSkeletonModule, NzTagModule, TranslatePipe,
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly message = inject(NzMessageService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  /** Patrz App.langLoaded — bez tego serie wykresow zostalyby na kluczach. */
  private readonly langLoaded = toSignal(this.translate.onLangChange, { initialValue: null });

  protected readonly quickActions = QUICK_ACTIONS;

  /** Akcja po kluczu, nie po indeksie — patrz komentarz przy actionByKey(). */
  protected readonly importAction = actionByKey('import-statement');

  /** Domyślnie ostatnie 30 dni — patrz DEFAULT_PERIOD_DAYS. */
  protected readonly range = signal<[Date, Date]>(defaultRange());

  /**
   * null = „nie wybrałem nic ręcznie", wtedy o budżecie domyślnym decyduje backend.
   * Typ `string`, bo to `BusinessId` (Guid) — jedyny publiczny identyfikator budżetu
   * (patrz `BudgetOption.id`), nie klucz z bazy.
   */
  private readonly budgetId = signal<string | null>(null);

  /**
   * Zapytanie jest funkcją sygnałów — zmiana zakresu albo budżetu sama je przeładowuje.
   * Bez ręcznego subscribe i bez trzymania loading/error/data w trzech osobnych sygnałach
   * (docs/anti-patterns.md → „RxJS for simple UI state (Angular 22)").
   */
  private readonly dashboard = httpResource<DashboardResponse>(() => ({
    url: '/api/dashboard',
    params: {
      from: toIsoDate(this.range()[0]),
      to: toIsoDate(this.range()[1]),
      ...(this.budgetId() !== null ? { budgetId: this.budgetId()! } : {}),
    },
  }));

  /**
   * ⚠️ `valueOf`, nie `this.dashboard.value` — ten drugi RZUCA w stanie błędu, a szablon czyta
   * `data()` TAKŻE poza gałęzią `@if (failed())`. Efekt był taki, że przy padniętym API
   * komunikat o błędzie owszem się pokazywał, ale render wywalał się zaraz pod nim i spinner
   * zostawał zapalony na zawsze. Patrz `core/api/resource-value.ts`.
   */
  protected readonly data = valueOf(this.dashboard);
  protected readonly loading = this.dashboard.isLoading;
  protected readonly failed = computed(() => this.dashboard.error() !== undefined);

  /**
   * Punkt startowy „szerokiego" okna pod wykres — liczony RAZ, przy starcie komponentu, nie
   * przy każdym odczycie: `new Date()` wywoływane na bieżąco dawałoby za KAŻDYM razem NOWY
   * obiekt, więc `chartData` (httpResource, niżej) myślałby, że parametry zapytania się
   * zmieniły, i refetchował w kółko — ta sama pułapka referencyjna, co przy `range.set()`
   * (patrz strażnik w `applyRange`).
   */
  private readonly chartRange: readonly [Date, Date] = (() => {
    const to = new Date();
    const from = new Date(to);
    from.setFullYear(from.getFullYear() - 1);
    return [from, to];
  })();

  /**
   * Wykres „Stan budżetu" dostaje WŁASNY, SZEROKI fetch (rok wstecz od dziś) zamiast dzielić
   * się z resztą dashboardu wąskim `range()` z Konfiguracji. Dzięki temu zoom/pan/reset na
   * wykresie są w 100% operacjami PO STRONIE KLIENTA nad już wczytanymi punktami — apex nigdy
   * nie dostaje NOWYCH `series`/`categories` w trakcie tych gestów, więc nie ma czego dociągać.
   * Ustalenie usera (2026-09-04): „pobierasz transakcje z całego roku do pamięci i wtedy
   * wykres nie musi nic doładowywać" — zgłoszone po tym, jak pan („poruszanie się po
   * wykresie") w ogóle nie działał: dawne `onChartZoom` snapowało wyłącznie do punktów z
   * WĄSKIEGO `range()`-owego `data()`, więc nie dało się wyjść poza aktualnie oglądane okno.
   *
   * Zależy od `selectedBudgetId()` — bezpiecznie, bo TEN sygnał jest teraz stabilny (patrz
   * doc przy `restoreRangeOnFirstLoad`: budżet domyślny zostaje PRZYPIĘTY zaraz po pierwszym
   * rozstrzygnięciu, więc już się nie przesuwa PRZY OKAZJI zawężania `range()`). Wykres i
   * Konfiguracja to jeden wspólny stan, nie dwa niezależne — user (2026-09-04): „wszystko się
   * opiera o to, jaki fragment jest zaznaczony na wykresie, on jest jak panel sterowania na
   * równi z konfiguracją" — gdyby budżet mógł się między nimi rozjechać, przestałyby być
   * jednym panelem.
   *
   * Świadomie ten sam endpoint `/api/dashboard`, z szerszym oknem — wyciągamy z odpowiedzi
   * tylko `budgetProgress`. Nadmiarowe (liczy też metryki/kategorie dla całego roku, których
   * nie używamy), ale to jedno zapytanie NA ZMIANĘ BUDŻETU, nie na każdy gest myszą — i nie ma
   * sensu dokładać osobnego endpointu, dopóki to naprawdę nie zaboli (CLAUDE.md §10).
   */
  private readonly chartData = httpResource<DashboardResponse>(() => {
    const id = this.selectedBudgetId();
    if (id === null) return undefined;

    return {
      url: '/api/dashboard',
      params: { from: toIsoDate(this.chartRange[0]), to: toIsoDate(this.chartRange[1]), budgetId: id },
    };
  });

  /** Bezpieczny odczyt — `value()` RZUCA w stanie błędu (patrz core/api/resource-value.ts). */
  private readonly chartDataValue = valueOf(this.chartData);

  /**
   * Wybór użytkownika — jawny (dropdown) albo PRZYPIĘTY przy pierwszym rozstrzygnięciu przez
   * backend (patrz `restoreRangeOnFirstLoad`). `budgetId()` NIE zostaje `null` na zawsze: od
   * momentu przypięcia to on jest źródłem prawdy, więc `selectedBudgetId()` przestaje podążać
   * za `data()?.selectedBudgetId` (który potrafi się przesuwać z każdą zmianą `range()` — to
   * backend liczy default OD NOWA przy każdym zapytaniu, patrz GetDashboardQueryHandler). Bez tego
   * przypięcia wykres i Konfiguracja mogłyby pokazywać RÓŻNE budżety naraz po samym zoomie.
   */
  protected readonly selectedBudgetId = computed(
    () => this.budgetId() ?? this.data()?.selectedBudgetId ?? null,
  );

  /**
   * Stan pustej aplikacji z makiety „Dashboard - Brak budżetu". Wyzwala go BRAK BUDŻETU,
   * a nie brak transakcji — alert w makiecie mówi wprost: „Przejdź do akcji Utwórz budżet
   * aby móc dodawać transakcje". Bez budżetu nie ma czego liczyć, więc metryki i wykresy
   * zastępujemy skeletonami, a nie zerami (zera wyglądałyby jak policzony wynik).
   */
  /**
   * Czy wybrany budzet jest wylaczony. Nie ukrywamy go z selektora: dane historyczne
   * ogląda się także po zamknięciu budżetu. Gasimy za to akcje, ktore DOPISUJA dane —
   * import na taki budzet i tak skonczylby sie odmowa z serwera (409).
   */
  protected readonly selectedBudgetDisabled = computed(() => {
    const id = this.selectedBudgetId();
    return (this.data()?.budgets ?? []).some((b) => b.id === id && b.disabled);
  });

  protected readonly noBudget = computed(() => {
    const d = this.data();
    return d !== undefined && d.budgets.length === 0;
  });

  /**
   * Budżet już jest, ale WYBRANY budżet nie ma ANI JEDNEJ transakcji — `hasAnyTransactions`
   * liczy się po stronie backendu w zasięgu tego jednego budżetu (patrz doc na
   * `HasAnyTransactions` w Dashboard/Contracts/DashboardResponse.cs), więc inny budżet z danymi nie gasi tego
   * stanu. Tego stanu NIE MA w makiecie (zgłoszone w issue); trzyma wzorzec „braku budżetu".
   */
  protected readonly noTransactions = computed(() => {
    const d = this.data();
    return d !== undefined && d.budgets.length > 0 && !d.hasAnyTransactions;
  });

  /** Oba stany puste zastępują dane skeletonami — jeden przełącznik dla szablonu. */
  protected readonly isBlank = computed(() => this.noBudget() || this.noTransactions());

  protected readonly hasDataInPeriod = computed(() => (this.data()?.byCategory.length ?? 0) > 0);

  protected readonly categoryChart = computed(() => {
    this.langLoaded();
    const rows = this.data()?.byCategory ?? [];
    return {
      series: [{ name: this.translate.instant('dashboard.charts.expensesSeries'), data: rows.map((r) => r.amount) }],
      chart: {
        type: 'bar' as const, height: 380, toolbar: { show: false }, fontFamily: 'inherit',
        events: {
          // Klik w słupek kategorii otwiera listę transakcji przefiltrowaną do TEJ kategorii,
          // TEGO okresu i TEGO budżetu — ustalenie usera (2026-09-04), patrz plans/transaction-list.md.
          // Trzeci argument jest opcjonalny w typach apexcharts — stąd `?.` i wczesny return.
          dataPointSelection: (_e: MouseEvent, _ctx: unknown, opts?: { dataPointIndex: number }) => {
            if (opts) this.onCategoryBarClick(opts.dataPointIndex);
          },
        },
      },
      plotOptions: { bar: { columnWidth: '55%', borderRadius: 2, distributed: true } },
      colors: CATEGORY_SERIES_COLORS,
      legend: { show: false },
      dataLabels: { enabled: false },
      xaxis: { categories: rows.map((r) => r.categoryName) },
      yaxis: { labels: { formatter: (v: number) => formatPln(v) } },
      tooltip: { y: { formatter: (v: number) => formatPln(v) } },
      grid: { borderColor: CHART_COLORS.neutral200 },
    };
  });

  protected readonly budgetChart = computed(() => {
    this.langLoaded();
    // `chartData`, NIE `data()` — patrz doc przy `chartData`: wykres rysuje z WŁASNEGO,
    // szerokiego (rok) fetchu, żeby zoom/pan/reset nie zależały od wąskiego okna Konfiguracji.
    const points = this.chartDataValue()?.budgetProgress ?? [];
    return {
      // Jedna seria: stan budżetu PO KAŻDEJ TRANSAKCJI. Wcześniej były dwie — narastające
      // wydatki i płaska linia limitu — co pokazywało, ile wydano w oknie, a nie ile jest
      // na koncie; a punkty były jeden na kalendarzowy dzień, nie jeden na transakcję.
      series: [
        {
          name: this.translate.instant('dashboard.charts.balanceSeries'),
          type: 'area' as const,
          data: points.map((p) => p.balance),
        },
      ],
      chart: {
        type: 'area' as const, height: 380, fontFamily: 'inherit',
        // ⚠️ `toolbar: { show: false }` (jak na drugim wykresie) WYŁĄCZA samo przeciąganie
        // zaznaczenia, nie tylko ikonki — sprawdzone na żywo: z `show:false` `events.zoomed`
        // NIGDY się nie odpalał, mimo `zoom.enabled: true`. Toolbar zostaje więc WIDOCZNY
        // (na życzenie usera, zamiast własnego przycisku „cofnij zaznaczenie") — to jego
        // ikonki zoom/pan/reset są jedynym UI do tych operacji.
        toolbar: { show: true, autoSelected: 'zoom' as const },
        // Zoom (drag-select bezpośrednio na wykresie) MUSI być włączony na osi 'x' — to on
        // daje `events.zoomed`, którym zaznaczenie zmienia realny zakres w Konfiguracji
        // (patrz onChartZoom), a nie tylko wizualnie powiększa istniejące punkty.
        // `autoScaleYaxis` łagodzi „podwójne szarpnięcie" przy przejściu do nowych danych.
        zoom: { enabled: true, type: 'x' as const, autoScaleYaxis: true },
        events: {
          zoomed: (_ctx: unknown, opts: { xaxis: { min: number; max: number } }) =>
            this.onChartZoom(opts.xaxis.min, opts.xaxis.max),
          // Ikonka „pan" (przesuwanie, nie zoom) przesuwa widoczne okno bez zmiany jego
          // szerokości — to osobny event (`scrolled`), nie `zoomed`, ale kształt taki sam
          // (min/max nowego okna), więc leci przez ten sam handler.
          scrolled: (_ctx: unknown, opts: { xaxis: { min: number; max: number } }) =>
            this.onChartZoom(opts.xaxis.min, opts.xaxis.max),
          // Ikonka „reset" (koło ze strzałką) NIE ma tu własnego hooka — `beforeResetZoom`
          // sprawdzone na żywo jako niewiarygodne (patrz doc przy onChartAreaClick). Klik na
          // reset łapiemy inaczej: bąbelkującym `(click)` na `.card` w dashboard.html.
        },
      },
      colors: [CHART_COLORS.primary500],
      // ⚠️ NIE 'smooth' — sprawdzone na żywo: krzywa sklejana (spline) apexa potrafi
      // PRZESTRZELIĆ poza rzeczywiste wartości sąsiednich punktów przy ostrym odwróceniu
      // kierunku (kilka transakcji z rzędu w dół, zaraz potem duży wpływ w górę — dokładnie
      // kształt, jaki mają dni wypłaty w tych danych). User złapał dołek na wykresie, który
      // schodził PONIŻEJ najniższej realnej transakcji tego dnia — 'smooth' go WYMYŚLAŁ,
      // żaden rekord mu nie odpowiadał. 'straight' nigdy nie wychodzi poza zakres dwóch
      // łączonych punktów, więc linia zawsze odpowiada faktycznym saldom.
      stroke: { curve: 'straight' as const, width: 2 },
      fill: { type: 'gradient', gradient: { opacityFrom: 0.35, opacityTo: 0.05 } },
      dataLabels: { enabled: false },
      // Oś CZASU, nie kategorii: punkty leżą teraz w nierównych odstępach (dzień bez
      // transakcji nie ma punktu), więc kategorie równo rozstawione po indeksie
      // kłamałyby o realnym czasie między nimi. Timestampy to lokalna północ
      // (fromIsoDate) — `datetimeUTC: false` jest NIEODZOWNE, inaczej apex formatuje
      // etykiety w UTC i przy dodatnim offsecie strefy pokazywałby dzień wcześniejszy
      // (dokładnie ta sama pułapka co przy toIsoDate/fromIsoDate gdzie indziej w kodzie).
      //
      // Świadomie BEZ tickAmount/rotate/hideOverlappingLabels na sztywno: apex sam dobiera
      // gęstość etykiet do szerokości widocznego okna — ogólnie (tygodnie/miesiące) przy
      // szerokim zakresie, szczegółowo (dni) po zawężeniu zaznaczeniem albo pickerem.
      xaxis: {
        type: 'datetime' as const,
        categories: points.map((p) => fromIsoDate(p.date).getTime()),
        labels: { datetimeUTC: false },
      },
      yaxis: { labels: { formatter: (v: number) => formatPln(v) } },
      tooltip: {
        shared: true,
        x: { format: 'd MMM yyyy' },
        y: { formatter: (v: number) => formatPln(v) },
      },
      // Zero jest tu punktem odniesienia: poniżej tej linii budżet jest pod kreską.
      annotations: {
        yaxis: [{ y: 0, borderColor: CHART_COLORS.error500, strokeDashArray: 4 }],
      },
      grid: { borderColor: CHART_COLORS.neutral200 },
    };
  });

  protected reload(): void {
    this.dashboard.reload();
  }

  /** Referencja do apexowego komponentu — potrzebna do PROGRAMOWEGO zoomu (patrz syncChartToRange). */
  private readonly budgetChartComponent = viewChild<ChartComponent>('budgetChartComponent');

  /**
   * Wykres i Konfiguracja to JEDEN panel sterowania, nie dwa niezależne — user (2026-09-04):
   * „wszystko się opiera o to, jaki fragment jest zaznaczony na wykresie, on jest jak panel
   * sterowania na równi z konfiguracją". Dotąd synchronizacja szła tylko w JEDNĄ stronę
   * (zoom/pan na wykresie → `range()` → Konfiguracja); ten efekt domyka drugi kierunek:
   * KAŻDA zmiana `range()` — ręczny picker, przywrócony zapamiętany zakres, zoom-out, reset —
   * musi WIZUALNIE przesunąć okno wykresu, żeby oba panele zawsze pokazywały to samo.
   *
   * `zoomX()` idzie DOKŁADNIE tą samą wewnętrzną ścieżką co przeciągnięcie myszą
   * (`Toolbar.zoomUpdateOptions` → `_updateOptions` → `events.zoomed`, sprawdzone w źródle
   * apexa) — więc bezpiecznie odpala z powrotem `onChartZoom`, ale strażnik dzień-do-dnia w
   * `applyRange` gasi to jako no-op, gdy `zoomX` i tak trafia w te same daty (czyli prawie
   * zawsze, poza pierwszym wywołaniem po zmianie z zewnątrz) — bez ryzyka pętli.
   */
  private readonly syncChartToRange = effect(() => {
    const [from, to] = this.range();
    const chart = this.budgetChartComponent();
    if (!chart) return;

    // Zaokrąglenie do lokalnej północy — ta sama jednostka, w której leżą punkty wykresu
    // (fromIsoDate), żeby zoom trafiał dokładnie w granice dni, nie w przypadkową godzinę.
    chart.zoomX(fromIsoDate(toIsoDate(from)).getTime(), fromIsoDate(toIsoDate(to)).getTime());
  });

  /**
   * Zakres SPRZED zaznaczenia na wykresie — stan pod ikonkę „reset" apexowego toolbara
   * (patrz `beforeResetZoom` w budgetChart). Jeden poziom (nie stos): druga, kolejna zmiana
   * zaznaczenia NIE nadpisuje go ponownie (patrz onChartZoom) — reset ma wracać do tego, co
   * było PRZED CAŁĄ serią przeciągnięć/zoomów na tym wykresie, nie tylko do ostatniego kroku.
   */
  private readonly rangeBeforeChartZoom = signal<[Date, Date] | null>(null);

  /**
   * Wspólna ścieżka dla KAŻDEJ zmiany zakresu — ręcznej w pickerze i z zaznaczenia na
   * wykresie: ustawia sygnał i zapamiętuje go pod aktualnym budżetem (patrz
   * budget-range-memory.ts). Zaznaczenie na wykresie ma być zapamiętane tak samo jak ręczna
   * zmiana — to wciąż „zakres, na którym user jest", niezależnie skąd przyszła zmiana.
   *
   * ⚠️ Strażnik „bez realnej zmiany dnia — nic nie rób" zostaje mimo że wykres ma już WŁASNY,
   * stabilny fetch (`chartData`, patrz doc tam) — chroni przed inną, wciąż realną ścieżką:
   * zmiana budżetu (świadoma albo pochodna, patrz `onBudgetChange`) WYMIENIA `chartData` na
   * inny zestaw punktów, a apex na taką podmianę potrafi odpowiedzieć WŁASNYM `zoomed` z
   * min/max odpowiadającym świeżo wczytanym granicom. Bez tego strażnika `range.set()` (NOWY
   * obiekt `[Date,Date]`, więc sygnał i tak uznaje to za zmianę, nawet gdy DATY są identyczne)
   * mógłby polecieć niepotrzebnie — a historycznie, zanim wykres dostał własny fetch, to była
   * DOSŁOWNA przyczyna nieskończonej pętli zapytań do API przy zwykłym zoom-in.
   */
  private applyRange(range: [Date, Date]): void {
    const [from, to] = range;
    const [curFrom, curTo] = this.range();
    if (toIsoDate(from) === toIsoDate(curFrom) && toIsoDate(to) === toIsoDate(curTo)) return;

    this.range.set(range);

    // Picker jest zablokowany bez wybranego budżetu, a zoom na wykresie nie działa bez
    // danych do pokazania — `selectedBudgetId()` jest więc w obu ścieżkach rozstrzygnięte.
    const id = this.selectedBudgetId();
    if (id !== null) rememberRange(id, range);
  }

  protected onRangeChange(value: Date[]): void {
    if (value?.length !== 2) return;
    this.applyRange([value[0], value[1]]);

    // Ręczna zmiana zakresu unieważnia „cofnij zaznaczenie" — ten przycisk ma sens
    // wyłącznie względem zaznaczenia zrobionego NA WYKRESIE, nie po tym, jak user
    // sam świadomie przestawił picker na coś innego.
    this.rangeBeforeChartZoom.set(null);
  }

  /**
   * Przeciągnięcie zaznaczenia LUB przesunięcie (pan) na wykresie „Stan budżetu"
   * (`chart.events.zoomed`/`scrolled`, patrz budgetChart) snapuje timestampy do najbliższych
   * realnych punktów i podstawia je jako PRAWDZIWY zakres w Konfiguracji — nie tylko wizualny
   * gest po stronie klienta. Snapujemy do punktów z `chartData` (SZEROKI, roczny zbiór), nie
   * `data()` — inaczej pan/zoom nie dałyby się wyprowadzić poza wąskie okno Konfiguracji
   * (dokładnie to zgłosił user: „poruszanie się po wykresie nie działa"). Zbyt wąskie
   * zaznaczenie (węższe niż odstęp między dwoma sąsiednimi punktami) `snapToAvailableRange`
   * zwraca jako `null` — wtedy świadomie nic nie robimy.
   */
  private onChartZoom(minMs: number, maxMs: number): void {
    const snapped = snapToAvailableRange(this.chartDataValue()?.budgetProgress ?? [], minMs, maxMs);
    if (!snapped) return;

    if (this.rangeBeforeChartZoom() === null) this.rangeBeforeChartZoom.set(this.range());
    this.applyRange(snapped);
  }

  /**
   * Klik na ikonkę „reset" apexowego toolbara. `@HostListener` (nie `(click)` w szablonie na
   * konkretnym elemencie): apex wstrzykuje toolbar jako WŁASNY DOM wewnątrz `apx-chart`, więc
   * łapiemy klik przez bąbelkowanie zdarzeń do hosta komponentu — bez udawania, że CAŁA karta
   * wykresu jest jednym interaktywnym elementem (czego chciałby a11y linter przy `(click)` w
   * szablonie, a co nie byłoby prawdą — tylko ikonka reset ma tu znaczenie).
   *
   * ⚠️ NIE polegamy na `chart.events.beforeResetZoom` — sprawdzone na żywo: apex NIE wywołuje
   * go po tym, jak my sami podmienimy dane na węższy zakres (`applyRange` → nowy `range` →
   * nowy fetch → NOWE `series`/`categories`). Apex traktuje wtedy bieżący, węższy zestaw jako
   * CAŁOŚĆ danych (`w.globals.zoomed` wraca na `false`), więc z jego perspektywy nie ma czego
   * resetować — klik w jego własną ikonkę reset staje się cichym no-opem.
   */
  @HostListener('click', ['$event'])
  protected onHostClick(event: MouseEvent): void {
    const target = event.target as HTMLElement | null;
    if (target?.closest('.apexcharts-reset-icon')) {
      this.resetChartSelection();
    } else if (target?.closest('.apexcharts-zoomout-icon')) {
      // ⚠️ Ikonka „zoom out" NIE idzie przez `chart.events.zoomed` (jak drag-zoom/pan) —
      // `snapToAvailableRange` snapuje wyłącznie do punktów JUŻ WCZYTANYCH, więc nie potrafi
      // wyjść POZA aktualnie załadowany zakres: apexowe „oddal" kończyłoby się zawsze na tych
      // samych granicach, czyli w praktyce niczym (dokładnie to zgłosił user). Oddalenie MUSI
      // więc liczyć nowy zakres samo, niezależnie od tego, co jest już wczytane w wykresie.
      this.zoomOutRange();
    }
  }

  /** Wraca DOKŁADNIE do zakresu sprzed zaznaczenia na wykresie — patrz rangeBeforeChartZoom. */
  private resetChartSelection(): void {
    const before = this.rangeBeforeChartZoom();
    if (before === null) return;

    this.rangeBeforeChartZoom.set(null);
    this.applyRange(before);
  }

  /**
   * Oddalenie zakresu o miesiąc w obie strony, aż `to` dojdzie do dzisiejszej daty — dalej
   * oddala się WYŁĄCZNIE wstecz, po 2 miesiące na klik (ustalenie usera, 2026-09-04).
   * `to` nigdy nie wychodzi w przyszłość — nie ma czego tam pokazywać.
   */
  private zoomOutRange(): void {
    const [from, to] = this.range();
    const today = new Date();
    const reachedToday = toIsoDate(to) >= toIsoDate(today);

    const newFrom = addMonths(from, reachedToday ? -2 : -1);
    const widerTo = reachedToday ? today : addMonths(to, 1);
    const newTo = toIsoDate(widerTo) > toIsoDate(today) ? today : widerTo;

    if (this.rangeBeforeChartZoom() === null) this.rangeBeforeChartZoom.set(this.range());
    this.applyRange([newFrom, newTo]);
  }

  /**
   * Świadomy wybór budżetu z listy: PRZYPINA go jako `budgetId()` (patrz doc na
   * `selectedBudgetId` — od tego staje się jedynym źródłem prawdy, backend przestaje dobierać
   * default za każdym zapytaniem) i podstawia zakres dat ZAPAMIĘTANY dla TEGO budżetu, jeśli
   * taki jest — inaczej user wracałby zawsze do zakresu zostawionego na poprzednim budżecie,
   * co nie jest tym samym co „pamięta zakres per budżet". Brak zapamiętanego zakresu = nic nie
   * ruszamy, zamiast ciągnąć z powrotem do domyślnych 30 dni bez powodu.
   *
   * Unieważnia też „cofnij zaznaczenie" — zaznaczenie na wykresie POPRZEDNIEGO budżetu nie ma
   * nic wspólnego z tym, co user właśnie zobaczy.
   */
  protected onBudgetChange(id: string | null): void {
    this.budgetId.set(id);
    this.rangeBeforeChartZoom.set(null);

    if (id === null) return;
    const remembered = loadRememberedRange(id);
    if (remembered) this.applyRange(remembered);
  }

  /**
   * Analogicznie do `onBudgetChange`, ale dla PIERWSZEGO rozstrzygnięcia budżetu domyślnego
   * przez backend (user nic jeszcze nie kliknął — `budgetId()` wciąż `null`, `selectedBudgetId()`
   * czyta z odpowiedzi backendu). Leci raz — `effect()` sam się niszczy po pierwszym
   * uruchomieniu — i za tym jednym razem PRZYPINA rozstrzygnięty budżet do `budgetId()`.
   *
   * ⚠️ To przypięcie jest tu NIEODZOWNE, nie kosmetyczne — sprawdzone na żywo jako źródło
   * DWÓCH niezależnych błędów, zanim je dodano: (1) nieskończona pętla `A→B→A→B` — bez
   * przypięcia `selectedBudgetId()` mógłby się dalej przesuwać z każdą zmianą `range()`
   * (backend liczy default OD NOWA przy każdym zapytaniu), a osobny efekt próbujący
   * przywrócić zapamiętany zakres INNEGO budżetu przy każdym takim przesunięciu potrafił
   * wciągnąć zakres, który z powrotem przesuwał default na poprzedni budżet — w kółko; (2)
   * wykres i Konfiguracja pokazujące RÓŻNE budżety naraz po samym zoomie na wykresie, wbrew
   * założeniu usera (2026-09-04): „wszystko się opiera o to, jaki fragment jest zaznaczony na
   * wykresie, on jest jak panel sterowania na równi z konfiguracją" — jeden panel sterowania
   * nie może po cichu rozjechać się w dwa.
   */
  private readonly restoreRangeOnFirstLoad = effect(() => {
    const id = this.selectedBudgetId();
    if (id === null) return;

    this.budgetId.set(id);

    const remembered = loadRememberedRange(id);
    if (remembered) this.applyRange(remembered);

    this.restoreRangeOnFirstLoad.destroy();
  });

  /**
   * Kafel wygaszamy, gdy jego akcja nie ma w danym stanie sensu:
   * bez budżetu nie da się dodać transakcji (makieta: #F7FAF8 / #DFDFDF),
   * a bez transakcji nie ma czego listować ani przeglądać.
   */
  protected isActionDisabled(action: SystemAction): boolean {
    if (this.noBudget()) return action.key === 'add-transaction';

    // Wylaczony budzet nie przyjmuje nowych danych — patrz selectedBudgetDisabled().
    if (this.selectedBudgetDisabled()) {
      if (action.key === 'add-transaction' || action.key === 'import-statement') return true;
    }

    if (this.noTransactions()) {
      return action.key === 'transaction-list' || action.key === 'review-queue';
    }
    return false;
  }

  protected runAction(action: SystemAction): void {
    if (this.isActionDisabled(action)) return;

    // Akcje bez trasy to te, których ekrany jeszcze nie powstały — mówimy o tym
    // wprost, zamiast udawać nawigację (patrz SystemAction.route).
    if (action.route === null) {
      this.message.info(
        this.translate.instant('actions.notImplemented', {
          label: this.translate.instant(action.labelKey),
        }),
      );
      return;
    }

    void this.router.navigate([action.route]);
  }

  /**
   * Klik w słupek „Podział wydatków na kategorie" → lista transakcji z wyfiltrowaną
   * kategorią, okresem i budżetem. Przejście niesie CZTERY rzeczy, nie tylko kategorię:
   * budżet i okres — inaczej lista pokazałaby inne dane niż to, z czego policzony jest
   * słupek — i `direction=expense`, bo słupki liczą WYŁĄCZNIE wydatki (GetDashboardQueryHandler);
   * bez tego lista domieszałaby wpływy tej samej kategorii, a jej suma nie zgodziłaby się
   * z wysokością słupka, w który user kliknął.
   *
   * `categoryId === null` to kubełek „bez kategorii" (sentinel, patrz CategorySpend) —
   * jedzie jako `uncategorized=true`, NIGDY jako przetłumaczona nazwa (przełączenie
   * języka rozsypałoby filtr oparty na tekście).
   */
  private onCategoryBarClick(pointIndex: number): void {
    const row = (this.data()?.byCategory ?? [])[pointIndex];
    const budgetId = this.selectedBudgetId();
    if (!row || budgetId === null) return;

    const [from, to] = this.range();
    void this.router.navigate(['/transactions'], {
      queryParams: {
        budgetId,
        from: toIsoDate(from),
        to: toIsoDate(to),
        direction: 'Expense',
        ...(row.categoryId !== null ? { categoryId: row.categoryId } : { uncategorized: true }),
      },
    });
  }

  /**
   * Formatowanie kwot w jednym miejscu. Pipe `number` zwraca `string | null`,
   * a `nz-statistic` nie przyjmuje null — stąd formatowanie w TS, nie w szablonie.
   */
  protected money(value: number | undefined | null): string {
    return new Intl.NumberFormat('pl-PL', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value ?? 0);
  }
}

/**
 * Musi odpowiadać DashboardPeriod.DefaultDays po stronie API — inaczej ekran pokazywałby
 * inny okres niż ten, który backend liczy, gdy parametry są pominięte.
 */
const DEFAULT_PERIOD_DAYS = 30;

/**
 * Kroczące 30 dni, NIE miesiąc kalendarzowy. Zakres „od 1. dnia miesiąca do dziś"
 * pierwszego dnia miesiąca obejmował jeden dzień, więc dashboard pokazywał same zera
 * mimo pełnej historii — dokładnie ten obraz, przed którym ostrzega issue.
 */
function defaultRange(): [Date, Date] {
  const to = new Date();
  const from = new Date(to);
  from.setDate(from.getDate() - (DEFAULT_PERIOD_DAYS - 1));
  return [from, to];
}

/** Kalendarzowy miesiąc (nie 30 dni na sztywno) — pod krok „oddal zakres" (zoomOutRange). */
function addMonths(date: Date, months: number): Date {
  const d = new Date(date);
  d.setMonth(d.getMonth() + months);
  return d;
}

function formatPln(value: number): string {
  return new Intl.NumberFormat('pl-PL', {
    style: 'currency',
    currency: 'PLN',
    maximumFractionDigits: 0,
  }).format(value ?? 0);
}
