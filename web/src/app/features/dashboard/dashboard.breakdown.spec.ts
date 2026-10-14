import { Component, input } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { provideNzDateFnsAdapter } from 'ng-zorro-antd/core/time';
import { NgApexchartsModule } from 'ng-apexcharts';
import { APP_ICONS } from '../../core/icons';
import { DashboardResponse } from '../../core/api/models/dashboard-response';
import { Dashboard } from './dashboard';

/** Atrapa wykresu: Apex potrzebuje prawdziwego SVG, a tu liczy się lista pod nim. `zoomX` woła synchronizacja zakresu. */
// eslint-disable-next-line @angular-eslint/component-selector -- atrapa MUSI mieć selektor prawdziwego wykresu
@Component({ selector: 'apx-chart', template: '' })
class FakeChart {
  readonly series = input<unknown>();
  readonly chart = input<unknown>();
  readonly plotOptions = input<unknown>();
  readonly colors = input<unknown>();
  readonly legend = input<unknown>();
  readonly dataLabels = input<unknown>();
  readonly xaxis = input<unknown>();
  readonly yaxis = input<unknown>();
  readonly tooltip = input<unknown>();
  readonly grid = input<unknown>();
  readonly stroke = input<unknown>();
  readonly fill = input<unknown>();
  readonly annotations = input<unknown>();
  readonly markers = input<unknown>();
  readonly title = input<unknown>();
  readonly zooms: number[][] = [];

  zoomX(...range: number[]): void {
    this.zooms.push(range);
  }
}

/**
 * Rozbicie kategorii na produkty z paragonów pod wykresem wydatków (makieta Figma 447:18335). Wykres Apex jest podmieniony na
 * pusty element — sprawdzamy listę rozbicia, nie rysowanie słupków.
 */
describe('Dashboard — rozbicie kategorii na produkty', () => {
  let fixture: ComponentFixture<Dashboard>;
  let http: HttpTestingController;

  const response = (): DashboardResponse => ({
    metrics: {
      totalExpenses: 1500, totalIncome: 0, budgetBalance: 0, topCategoryName: 'Jedzenie', topCategoryAmount: 1000,
      largestExpenseDescription: null, largestExpenseAmount: 0, toReviewCount: 0, reservedOnRegular: 0,
    },
    byCategory: [
      {
        categoryId: 'c1', categoryName: 'Jedzenie', amount: 1000,
        products: [
          { productCategoryId: 'p1', name: 'Nabiał', amount: 400, kind: 'Category' },
          { productCategoryId: null, name: null, amount: 100, kind: 'Uncategorized' },
          { productCategoryId: null, name: null, amount: 500, kind: 'NoReceipt' },
        ],
      },
      { categoryId: 'c2', categoryName: 'Paliwo', amount: 500, products: [] },
    ],
    budgetProgress: [],
    recentTransactions: [],
    budgets: [{ id: 'b1', name: 'Domowy', month: '2026-10-01', disabled: false }],
    selectedBudgetId: 'b1',
    hasAnyTransactions: true,
  } as unknown as DashboardResponse);

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideNoopAnimations(),
        provideTranslateService(), provideNzIcons(APP_ICONS), provideNzI18n(pl_PL), provideNzDateFnsAdapter(),
      ],
    });
    TestBed.overrideComponent(Dashboard, {
      remove: { imports: [NgApexchartsModule] },
      add: { imports: [FakeChart] },
    });
    TestBed.inject(TranslateService).use('pl');
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();

    // Główne zapytanie i szeroki fetch wykresu startują w różnych momentach (ten drugi dopiero po rozstrzygnięciu budżetu).
    for (let round = 0; round < 4; round++) {
      for (const request of http.match((r) => r.url === '/api/dashboard')) request.flush(response());
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve));
    }
    fixture.detectChanges();
  });

  const text = () => fixture.nativeElement.textContent as string;
  const heads = () => Array.from(fixture.nativeElement.querySelectorAll('.dash__bd-head') as NodeListOf<HTMLButtonElement>);

  it('pokazuje rozbicie tylko dla kategorii, które mają produkty z paragonów', () => {
    // Łapie listę rozbicia z pustymi kategoriami: „Paliwo” nie ma paragonu, więc nie ma czego rozwijać.
    expect(heads().map((h) => h.textContent?.includes('Jedzenie'))).toEqual([true]);
  });

  it('jest zwinięte, a klik rozwija produkty z udziałem w kwocie kategorii i etykietami dla wierszy bez kategorii', () => {
    expect(fixture.nativeElement.querySelector('.dash__bd-list')).toBeNull();

    heads()[0].click();
    fixture.detectChanges();

    expect(heads()[0].getAttribute('aria-expanded')).toBe('true');
    const rows = Array.from(fixture.nativeElement.querySelectorAll('.dash__bd-row') as NodeListOf<HTMLElement>)
      .map((r) => (r.textContent ?? '').replace(/\s+/g, ' ').trim());
    expect(rows).toHaveLength(3);
    expect(rows[0]).toContain('Nabiał');
    expect(rows[0]).toContain('40%');
    // Etykiety dla wierszy bez nazwy tłumaczy front po rodzaju wiersza (w teście klucze, bo tłumaczeń nie ładujemy).
    expect(rows[1]).toContain('dashboard.breakdown.uncategorized');
    expect(rows[2]).toContain('dashboard.breakdown.noReceipt');
  });

  it('ponowny klik zwija rozbicie', () => {
    heads()[0].click();
    fixture.detectChanges();
    heads()[0].click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.dash__bd-list')).toBeNull();
    expect(text()).toContain('dashboard.breakdown.title');
  });
});
