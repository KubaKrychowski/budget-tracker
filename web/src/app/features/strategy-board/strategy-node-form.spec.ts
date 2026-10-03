import { registerLocaleData } from '@angular/common';
import pl from '@angular/common/locales/pl';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzDateFnsAdapter } from 'ng-zorro-antd/core/time';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { APP_ICONS } from '../../core/icons';
import { StrategyNode, StrategyNodeOutcome, StrategyProblemKind } from '../../core/api/models/strategies';
import { StrategyNodeForm } from './strategy-node-form';

registerLocaleData(pl);

/** Formularz zaznaczonego kafelka — pola zależą od rodzaju, a każda zmiana wraca do rodzica jako cały kafelek. */
describe('StrategyNodeForm', () => {
  let fixture: ComponentFixture<StrategyNodeForm>;

  const node = (over: Partial<StrategyNode> = {}): StrategyNode => ({
    id: 'n1', type: 'Overpay', title: 'Nadpłać kredyt', x: 0, y: 0, month: null, amount: 7000, rate: null,
    installment: null, mode: 'ReduceInstallment', metric: null, comparison: null, threshold: null, ...over,
  });

  const text = (): string => (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');
  const ids = (): string[] => [...fixture.nativeElement.querySelectorAll('[id^="node-"]')].map((e) => (e as HTMLElement).id);

  const mount = async (
    value: StrategyNode, outcome: StrategyNodeOutcome | null = null, problems: StrategyProblemKind[] = [],
  ): Promise<void> => {
    fixture = TestBed.createComponent(StrategyNodeForm);
    fixture.componentRef.setInput('node', value);
    fixture.componentRef.setInput('outcome', outcome);
    fixture.componentRef.setInput('problems', problems);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StrategyNodeForm],
      providers: [
        provideNoopAnimations(), provideTranslateService(), provideNzIcons(APP_ICONS), provideNzI18n(pl_PL),
        provideNzDateFnsAdapter(),
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('pl', {
      strategies: {
        board: {
          types: { Overpay: 'Nadpłać kredyt', Loan: 'Kredyt', Condition: 'Warunek', PayOffLoan: 'Spłać resztę' },
          kinds: { action: 'Akcja', base: 'Stan wyjściowy', condition: 'Warunek' },
          amount: { Overpay: 'Kwota nadpłaty (zł)', Loan: 'Saldo kredytu (zł)' },
          mode: { ReduceInstallment: 'Obniż ratę', ShortenPeriod: 'Skróć okres' },
          metric: { CashMinusDebt: 'Gotówka − dług' },
          comparison: { AtLeast: 'jest co najmniej' },
          form: { back: 'Dodaj kafelek', title: 'Podpis kafelka', remove: 'Usuń kafelek', done: 'Gotowe', month: 'Miesiąc', loanMonth: 'Miesiąc startu kredytu', rate: 'Oprocentowanie', installment: 'Rata', mode: 'Tryb rozliczenia', metric: 'Co porównujemy', comparison: 'Porównanie', threshold: 'Wartość' },
          outcome: { met: 'Spełniony: {{month}}', notMet: 'Nie został spełniony', fired: 'Wykonany: {{month}}' },
          problem: { MissingParameter: 'Brakuje parametru.', NoIncomingEdge: 'Żadna strzałka tu nie prowadzi.' },
        },
      },
    });
    translate.use('pl');
  });

  it('pokazuje tylko pola, które ma dany rodzaj kafelka', async () => {
    await mount(node({ type: 'Overpay' }));
    expect(ids()).toEqual(expect.arrayContaining(['node-title', 'node-amount']));
    expect(ids()).not.toContain('node-month');
    expect(ids()).not.toContain('node-rate');

    await mount(node({ type: 'Loan', month: '2026-10-01', amount: 18400, rate: 12, installment: 880 }));
    expect(ids()).toEqual(expect.arrayContaining(['node-title', 'node-month', 'node-amount', 'node-rate', 'node-installment']));
    expect(text()).toContain('Miesiąc startu kredytu');
  });

  it('warunek ma trzy pola: co, porównanie i wartość', async () => {
    await mount(node({ type: 'Condition', title: '', metric: 'CashMinusDebt', comparison: 'AtLeast', threshold: 9000 }));

    expect(ids()).toEqual(expect.arrayContaining(['node-metric', 'node-comparison', 'node-threshold']));
    expect(ids()).not.toContain('node-amount');
  });

  it('kafelek bez parametrów (spłata reszty) pokazuje tylko podpis', async () => {
    await mount(node({ type: 'PayOffLoan', title: '', amount: null, mode: null }));

    expect(ids()).toEqual(['node-title']);
  });

  it('pokazuje, kiedy kafelek się wykonał albo warunek został spełniony', async () => {
    await mount(node({ type: 'Overpay' }), { nodeId: 'n1', firedIn: '2027-05-01', conditionMetIn: null });
    expect(text()).toMatch(/Wykonany: maj 2027/);

    await mount(node({ type: 'Condition', title: '', metric: 'CashMinusDebt', comparison: 'AtLeast', threshold: 9000 }), {
      nodeId: 'n1', firedIn: '2027-05-01', conditionMetIn: '2027-07-01',
    });
    expect(text()).toMatch(/Spełniony: lipiec 2027/);
  });

  it('wymienia problemy kafelka pod polami', async () => {
    await mount(node(), null, ['MissingParameter', 'NoIncomingEdge']);

    expect(text()).toContain('Brakuje parametru.');
    expect(text()).toContain('Żadna strzałka tu nie prowadzi.');
  });

  it('zmiana podpisu wraca do rodzica jako cały kafelek z nową wartością', async () => {
    await mount(node());
    const emitted: StrategyNode[] = [];
    fixture.componentInstance.changed.subscribe((n) => emitted.push(n));

    const input = fixture.nativeElement.querySelector('#node-title') as HTMLInputElement;
    input.value = 'Nowy podpis';
    input.dispatchEvent(new Event('input'));

    expect(emitted).toHaveLength(1);
    expect(emitted[0]).toEqual({ ...node(), title: 'Nowy podpis' });
  });

  it('przyciski „Usuń kafelek”, „Gotowe” i „Dodaj kafelek” zgłaszają się do rodzica', async () => {
    await mount(node());
    const events: string[] = [];
    fixture.componentInstance.removed.subscribe(() => events.push('removed'));
    fixture.componentInstance.closed.subscribe(() => events.push('closed'));
    const buttons = [...fixture.nativeElement.querySelectorAll('button')] as HTMLButtonElement[];
    const click = (label: string): void => buttons.find((b) => b.textContent?.includes(label))!.click();

    click('Usuń kafelek');
    click('Gotowe');
    click('Dodaj kafelek');

    expect(events).toEqual(['removed', 'closed', 'closed']);
  });

  /**
   * Regresja: pole miesiąca dostawało przy każdym renderze NOWĄ instancję `Date`, więc `ngModel` widział „zmianę”,
   * emitował ją, to aktualizowało tablicę, formularz renderował się od nowa — w pętli bez końca, która wywracała
   * stronę po zaznaczeniu kafelka z miesiącem (np. kredytu). Bez żadnego ruchu użytkownika nic nie ma prawa wyjść.
   */
  it('kafelek z miesiącem nie emituje zmian sam z siebie, choćby formularz renderował się wiele razy', async () => {
    await mount(node({ type: 'Loan', title: 'Kredyt', month: '2026-10-01', amount: 18400, rate: 12, installment: 880 }));
    const emitted: StrategyNode[] = [];
    fixture.componentInstance.changed.subscribe((n) => emitted.push(n));

    for (let i = 0; i < 20; i++) {
      fixture.detectChanges();
      await fixture.whenStable();
    }

    expect(emitted).toEqual([]);
  });
});
