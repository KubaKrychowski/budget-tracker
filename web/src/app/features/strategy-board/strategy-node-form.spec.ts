import { registerLocaleData } from '@angular/common';
import pl from '@angular/common/locales/pl';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzDateFnsAdapter } from 'ng-zorro-antd/core/time';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { APP_ICONS } from '../../core/icons';
import { StrategyNode, StrategyNodeOutcome, StrategyProblemKind, StrategyReferences } from '../../core/api/models/strategies';
import { StrategyNodeForm } from './strategy-node-form';

registerLocaleData(pl);

/** Formularz zaznaczonego kafelka — pola zależą od rodzaju, a każda zmiana wraca do rodzica jako cały kafelek. */
describe('StrategyNodeForm', () => {
  let fixture: ComponentFixture<StrategyNodeForm>;

  const node = (over: Partial<StrategyNode> = {}): StrategyNode => ({
    id: 'n1', type: 'Overpay', title: 'Nadpłać kredyt', x: 0, y: 0, month: null, amount: 7000, rate: null,
    installment: null, mode: 'ReduceInstallment', metric: null, comparison: null, threshold: null, categoryId: null, standingOrderId: null, actualMonth: null, actualAmount: null, ...over,
  });

  const text = (): string => (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');
  const ids = (): string[] => [...fixture.nativeElement.querySelectorAll('[id^="node-"]')].map((e) => (e as HTMLElement).id);

  const mount = async (
    value: StrategyNode, outcome: StrategyNodeOutcome | null = null, problems: StrategyProblemKind[] = [],
    references: StrategyReferences | null = null,
  ): Promise<void> => {
    fixture = TestBed.createComponent(StrategyNodeForm);
    fixture.componentRef.setInput('node', value);
    fixture.componentRef.setInput('outcome', outcome);
    fixture.componentRef.setInput('problems', problems);
    fixture.componentRef.setInput('references', references);
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
          types: { Income: 'Wpływ', Expense: 'Wydatek', Trigger: 'Zdarzenie', Overpay: 'Nadpłać kredyt', Loan: 'Kredyt', Condition: 'Warunek', PayOffLoan: 'Spłać resztę' },
          kinds: { event: 'Zdarzenie', action: 'Akcja', base: 'Stan wyjściowy', condition: 'Warunek' },
          amount: { Income: 'Kwota wpływu', Expense: 'Kwota wydatku', Overpay: 'Kwota nadpłaty (zł)', Loan: 'Saldo kredytu (zł)' },
          mode: { ReduceInstallment: 'Obniż ratę', ShortenPeriod: 'Skróć okres' },
          metric: { CashMinusDebt: 'Gotówka − dług' },
          comparison: { AtLeast: 'jest co najmniej' },
          form: { fact: { title: 'Zdarzenie nastąpiło', amount: 'Faktyczna kwota', month: 'Faktyczny miesiąc', delta: 'Różnica względem planu: {{delta}} w gotówce.', hint: 'Plan zostaje do porównania.', off: 'Liczone z planu.' }, name: 'Nazwa', dueMonth: 'Termin', lastMonth: 'Ostatni miesiąc zlecenia', category: 'Kategoria', standingOrder: 'Zlecenie', warning: 'Ostrzegaj od', applyTitle: 'Zastosuj w aplikacji', categoryMissing: 'Tej kategorii nie ma już na liście.', standingOrderMissing: 'Tego zlecenia nie ma już na liście.', type: { event: 'Rodzaj zdarzenia', action: 'Rodzaj akcji', control: 'Rodzaj warunku' }, back: 'Dodaj kafelek', title: 'Podpis kafelka', remove: 'Usuń kafelek', done: 'Gotowe', month: 'Miesiąc', loanMonth: 'Miesiąc startu kredytu', rate: 'Oprocentowanie', installment: 'Rata', mode: 'Tryb rozliczenia', metric: 'Co porównujemy', comparison: 'Porównanie', threshold: 'Wartość' },
          applyInfo: { SetLimit: 'Ustawi limit {{amount}} zł dla „{{category}}”.', Overpay: 'x' },
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

  const refs: StrategyReferences = {
    categories: [{ id: 'c1', name: 'Jedzenie' }],
    standingOrders: [{ id: 'o1', name: 'Rata kredytu', expectedAmount: 880 }],
  };

  it('limit kategorii ma kategorię, kwotę i próg ostrzeżenia oraz ramkę „Zastosuj w aplikacji”', async () => {
    await mount(node({ type: 'SetLimit', title: '', amount: 1500, threshold: 90, categoryId: 'c1' }), null, [], refs);

    expect(ids()).toEqual(expect.arrayContaining(['node-category', 'node-amount', 'node-warning']));
    expect(text()).toContain('Zastosuj w aplikacji');
    expect(text()).toContain('Ustawi limit 1500 zł dla „Jedzenie”.');
  });

  it('zakończenie zlecenia stałego ma zlecenie i ostatni miesiąc, a bez kwoty', async () => {
    await mount(node({ type: 'EndStandingOrder', title: '', amount: null, month: '2027-07-01', standingOrderId: 'o1' }), null, [], refs);

    expect(ids()).toEqual(expect.arrayContaining(['node-order', 'node-month']));
    expect(ids()).not.toContain('node-amount');
    expect(text()).toContain('Ostatni miesiąc zlecenia');
  });

  it('rezerwacja i wydatek jednorazowy pytają o „Nazwę”, a nie o podpis kafelka', async () => {
    await mount(node({ type: 'CreateReservation', title: '', amount: 500 }), null, [], refs);
    expect(text()).toContain('Nazwa');
    expect(text()).not.toContain('Podpis kafelka');
    expect(ids()).toContain('node-due');

    await mount(node({ type: 'Overpay' }));
    expect(text()).toContain('Podpis kafelka');
  });

  it('ostrzega, gdy wskazanej kategorii albo zlecenia nie ma już na liście', async () => {
    await mount(node({ type: 'SetLimit', title: '', amount: 1500, categoryId: 'gone' }), null, [], refs);
    expect(text()).toContain('Tej kategorii nie ma już na liście.');

    await mount(node({ type: 'EndStandingOrder', title: '', amount: null, standingOrderId: 'gone' }), null, [], refs);
    expect(text()).toContain('Tego zlecenia nie ma już na liście.');

    await mount(node({ type: 'SetLimit', title: '', amount: 1500, categoryId: 'gone' }));
    expect(text()).not.toContain('Tej kategorii nie ma już na liście.');
  });

  it('akcja, która niczego nie zakłada w budżecie, nie ma ramki „Zastosuj w aplikacji”', async () => {
    await mount(node({ type: 'Overpay' }));

    expect(text()).not.toContain('Zastosuj w aplikacji');
  });

  it('warunek ma trzy pola: co, porównanie i wartość', async () => {
    await mount(node({ type: 'Condition', title: '', metric: 'CashMinusDebt', comparison: 'AtLeast', threshold: 9000 }));

    expect(ids()).toEqual(expect.arrayContaining(['node-metric', 'node-comparison', 'node-threshold']));
    expect(ids()).not.toContain('node-amount');
  });

  it('kafelek bez parametrów (spłata reszty) pokazuje tylko rodzaj i podpis', async () => {
    await mount(node({ type: 'PayOffLoan', title: '', amount: null, mode: null }));

    expect(ids()).toEqual(['node-type', 'node-title']);
  });

  it('select rodzaju pokazuje bieżący rodzaj, a podpis nad nim zależy od kategorii kafelka', async () => {
    await mount(node({ type: 'Overpay' }));

    expect(text()).toContain('Rodzaj akcji');
    expect(text()).toContain('Nadpłać kredyt');
  });

  it('rodzaje wybiera się w obrębie kategorii kafelka: akcja nie zamienia się w zdarzenie', async () => {
    await mount(node({ type: 'Overpay' }));
    const groups = (fixture.componentInstance as unknown as { typeGroups(): { key: string; types: { type: string }[] }[] }).typeGroups();

    expect(groups.map((g) => g.key)).toEqual(['simulated', 'budget']);
    expect(groups.flatMap((g) => g.types.map((t) => t.type))).toEqual([
      'IncreaseSurplus', 'Overpay', 'PayOffLoan', 'SetSavingsGoal', 'CreateReservation', 'SetLimit', 'EndStandingOrder', 'CreateEpisodicOrder',
    ]);
  });

  it('limit kategorii jest niedostępny, gdy do kafelka prowadzi wpływ jednorazowy', async () => {
    fixture = TestBed.createComponent(StrategyNodeForm);
    fixture.componentRef.setInput('node', node({ type: 'Overpay' }));
    fixture.componentRef.setInput('incomingTypes', ['Income']);
    fixture.detectChanges();
    const groups = (fixture.componentInstance as unknown as { typeGroups(): { types: { type: string; unavailable: string | null }[] }[] }).typeGroups();
    const types = groups.flatMap((g) => g.types);

    expect(types.find((t) => t.type === 'SetLimit')?.unavailable).toBe('strategies.board.unavailable.incomeLimit');
    expect(types.filter((t) => t.unavailable)).toHaveLength(1);
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

  describe('zdarzenie nastąpiło', () => {
    const income = (over: Partial<StrategyNode> = {}): StrategyNode =>
      node({ type: 'Income', title: 'Premia', month: '2027-05-01', amount: 7800, ...over });
    const toggle = (): HTMLButtonElement => fixture.nativeElement.querySelector('nz-switch button') as HTMLButtonElement;

    it('wpływ ma przełącznik, a akcja i kredyt go nie mają', async () => {
      await mount(income());
      expect(toggle()).not.toBeNull();
      expect(text()).toContain('Liczone z planu.');

      await mount(node({ type: 'Overpay' }));
      expect(toggle()).toBeNull();

      await mount(node({ type: 'Loan', month: '2026-10-01', amount: 18400, rate: 12, installment: 880 }));
      expect(toggle()).toBeNull();
    });

    it('włączenie przepisuje plan do faktu, wyłączenie czyści fakt', async () => {
      await mount(income());
      const emitted: StrategyNode[] = [];
      fixture.componentInstance.changed.subscribe((n) => emitted.push(n));

      toggle().click();
      expect(emitted.at(-1)).toMatchObject({ actualMonth: '2027-05-01', actualAmount: 7800 });

      await mount(income({ actualMonth: '2027-06-01', actualAmount: 8150 }));
      fixture.componentInstance.changed.subscribe((n) => emitted.push(n));
      toggle().click();
      expect(emitted.at(-1)).toMatchObject({ actualMonth: null, actualAmount: null });
    });

    it('zdarzenie bez kwoty (np. podwyżka) przepisuje sam miesiąc i nie pyta o kwotę faktu', async () => {
      await mount(node({ type: 'Trigger', month: '2027-01-01' }));
      const emitted: StrategyNode[] = [];
      fixture.componentInstance.changed.subscribe((n) => emitted.push(n));

      toggle().click();

      expect(emitted.at(-1)).toMatchObject({ actualMonth: '2027-01-01', actualAmount: null });
      await mount(node({ type: 'Trigger', month: '2027-01-01', actualMonth: '2027-03-01' }));
      expect(ids()).toContain('node-actual-month');
      expect(ids()).not.toContain('node-actual-amount');
    });

    it('po włączeniu pokazuje pola faktu i różnicę w gotówce: wpływ większy to plus, wydatek większy to minus', async () => {
      await mount(income({ actualMonth: '2027-06-01', actualAmount: 8150 }));
      expect(ids()).toEqual(expect.arrayContaining(['node-actual-amount', 'node-actual-month']));
      expect(text()).toContain('Różnica względem planu: +350 zł w gotówce.');

      await mount(node({ type: 'Expense', month: '2026-12-01', amount: 2200, actualMonth: '2026-12-01', actualAmount: 2350 }));
      expect(text()).toContain('Różnica względem planu: −150 zł w gotówce.');
    });

    it('bez różnicy w kwocie nie pokazuje wiersza różnicy', async () => {
      await mount(income({ actualMonth: '2027-05-01', actualAmount: 7800 }));

      expect(text()).not.toContain('Różnica względem planu');
    });

    it('bez miesiąca w planie przełącznik jest wyłączony — nie ma czego przepisać do faktu', async () => {
      await mount(income({ month: null }));

      expect(toggle().disabled).toBe(true);
    });
  });
});
