import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { StrategyResult } from '../../core/api/models/strategies';
import { CompareSeries, metricValue, niceStep, StrategyCompare } from './strategy-compare';

/** Okno porównania wariantów — wykres z linią na wariant, tabela dat i kwot z różnicą względem bazowego. */
describe('StrategyCompare', () => {
  let fixture: ComponentFixture<StrategyCompare>;

  const result = (over: Partial<StrategyResult> = {}): StrategyResult => ({
    months: [
      { month: '2026-10-01', cash: 1000, debt: 5000, interest: 50 },
      { month: '2026-11-01', cash: 2000, debt: 4000, interest: 40 },
      { month: '2026-12-01', cash: 3000, debt: 3000, interest: 30 },
    ],
    loanPaidOffIn: '2027-07-01', cushionReachedIn: null, finalCash: 17300, totalInterest: 1400, nodes: [], problems: [], ...over,
  });

  const base: CompareSeries = { id: null, name: 'Bazowy', result: result() };
  const raise: CompareSeries = {
    id: 'v1', name: 'Bez premii', result: result({ loanPaidOffIn: '2027-09-01', finalCash: 14900, totalInterest: 1550 }),
  };

  const text = (): string => (document.body.textContent as string).replace(/\s+/g, ' ');

  const render = async (series: CompareSeries[]): Promise<void> => {
    fixture.componentRef.setInput('series', series);
    fixture.componentRef.setInput('hasLoan', true);
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [StrategyCompare], providers: [provideNoopAnimations(), provideTranslateService()] }).compileComponents();
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('pl', {
      strategies: {
        compare: {
          title: 'Porównanie wariantów', titleSingle: 'Przebieg symulacji', alertTitle: 'To szacunek', alertBody: 'Odsetki wg daty.',
          caption: '{{metric}} na koniec miesiąca.', metrics: { Cash: 'Gotówka', Debt: 'Dług', CashMinusDebt: 'Gotówka − dług' },
          chart: 'Wykres: {{metric}}', measure: 'Miara', loanPaidOff: 'Kredyt spłacony', cushionReached: 'Poduszka', finalCash: 'Gotówka na koniec',
          interest: 'Odsetki', never: 'nie w tym horyzoncie', monthsShort: '{{delta}} mies.', close: 'Zamknij',
        },
      },
    });
    translate.use('pl');
    fixture = TestBed.createComponent(StrategyCompare);
  });

  afterEach(() => {
    fixture.destroy();
    document.body.querySelectorAll('.cdk-overlay-container').forEach((e) => e.replaceChildren());
  });

  it('z jedną serią to „Przebieg symulacji”', async () => {
    await render([base]);

    expect(text()).toContain('Przebieg symulacji');
    expect(text()).not.toContain('Porównanie wariantów');
  });

  it('z kilkoma seriami to „Porównanie wariantów”', async () => {
    await render([base, raise]);

    expect(text()).toContain('Porównanie wariantów');
  });

  it('rysuje jedną linię na serię i jeden punkt na miesiąc', async () => {
    await render([base, raise]);

    const lines = document.body.querySelectorAll('.scmp__chart polyline');
    expect(lines).toHaveLength(2);
    expect(lines[0].getAttribute('points')!.split(' ')).toHaveLength(base.result.months.length);
  });

  it('tabela pokazuje wartości każdego wariantu i różnicę względem bazowego', async () => {
    await render([base, raise]);

    expect(text()).toContain('lipiec 2027');
    expect(text()).toContain('wrzesień 2027');
    expect(text()).toContain('+2 mies.');
    expect(text()).toContain('14 900 zł');
    expect(text()).toContain('−2400 zł');
    expect(text()).toContain('+150 zł');
  });

  it('kredyt niespłacony w horyzoncie pokazuje „nie w tym horyzoncie” zamiast daty', async () => {
    await render([base, { ...raise, result: result({ loanPaidOffIn: null }) }]);

    expect(text()).toContain('nie w tym horyzoncie');
  });

  it('niceStep dobiera okrągły krok osi, a metricValue liczy trzy miary', () => {
    expect(niceStep(17300, 4)).toBe(5000);
    expect(niceStep(0, 4)).toBe(1);
    expect(metricValue('Cash', 10, 4)).toBe(10);
    expect(metricValue('Debt', 10, 4)).toBe(4);
    expect(metricValue('CashMinusDebt', 10, 4)).toBe(6);
  });
});
