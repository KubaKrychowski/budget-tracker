import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { APP_ICONS } from '../../core/icons';
import { StrategyApplyItem, StrategyApplyPreview } from '../../core/api/models/strategies';
import { StrategyApply } from './strategy-apply';

/** Okno „Zastosuj w budżecie” — statusy akcji, zaznaczanie tylko tego, co da się zastosować, i atomowe wysłanie wyboru. */
describe('StrategyApply', () => {
  let fixture: ComponentFixture<StrategyApply>;
  let http: HttpTestingController;

  const item = (over: Partial<StrategyApplyItem>): StrategyApplyItem => ({
    nodeId: 'n', type: 'SetSavingsGoal', title: '', amount: 700, status: 'New', month: '2026-10-01', currentAmount: null, ...over,
  });

  const preview = (items: StrategyApplyItem[]): StrategyApplyPreview => ({ budgetId: 'b1', budgetName: 'Domowy', items });

  const text = (): string => (document.body.textContent as string).replace(/\s+/g, ' ');
  const checkboxes = (): HTMLInputElement[] => [...document.body.querySelectorAll<HTMLInputElement>('.sap__check')];
  const okButton = (): HTMLButtonElement =>
    [...document.body.querySelectorAll<HTMLButtonElement>('.ant-modal-footer button')].find((b) => b.classList.contains('ant-btn-primary'))!;

  const open = async (items: StrategyApplyItem[]): Promise<void> => {
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/strategies/s1/apply' && r.method === 'GET').flush(preview(items));
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StrategyApply],
      providers: [
        provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), provideTranslateService(), provideNzIcons(APP_ICONS),
      ],
    }).compileComponents();

    TestBed.inject(TranslateService).setTranslation('pl', {
      strategies: {
        board: { types: { SetSavingsGoal: 'Cel', CreateReservation: 'Rezerwacja', SetLimit: 'Limit' } },
        apply: {
          title: 'Zastosuj strategię w budżecie', ok: 'Zastosuj zaznaczone ({{count}})', cancel: 'Anuluj', empty: 'Brak akcji.',
          alert: { one: 'Zmienisz {{count}} rzecz w „{{budget}}”', few: 'Zmienisz {{count}} rzeczy w „{{budget}}”', many: 'Zmienisz {{count}} rzeczy w „{{budget}}”', other: 'Zmienisz {{count}} rzeczy w „{{budget}}”' },
          alertBody: 'Nic nie zmieni się bez zatwierdzenia.',
          status: { New: 'nowa', Change: 'zmiana', Exists: 'już jest', Waiting: 'czeka', Incomplete: 'uzupełnij' },
          items: { SetSavingsGoal: 'Cel {{amount}}', CreateReservation: 'Rezerwacja „{{name}}”', SetLimit: 'Limit {{amount}}' },
          note: { SetSavingsGoal: { Change: 'Teraz {{current}}' } },
          noteStatus: { Exists: 'To już jest', Waiting: 'Czeka na miesiąc', Incomplete: 'Uzupełnij kafelek' },
          done: 'Zastosowano: {{count}}.', variant: 'Wariant: {{name}}',
        },
      },
    });
    TestBed.inject(TranslateService).use('pl');

    fixture = TestBed.createComponent(StrategyApply);
    fixture.componentRef.setInput('strategyId', 's1');
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.match(() => true);
    fixture.destroy();
    document.body.querySelectorAll('.cdk-overlay-container').forEach((e) => e.replaceChildren());
  });

  it('nie pyta serwera, dopóki okno jest zamknięte', () => {
    fixture.detectChanges();

    http.expectNone('/api/strategies/s1/apply');
  });

  it('zaznacza domyślnie tylko akcje „nowa” i „zmiana”, a pozostałe ma wyłączone', async () => {
    await open([
      item({ nodeId: 'a', status: 'New' }),
      item({ nodeId: 'b', status: 'Change', currentAmount: 400 }),
      item({ nodeId: 'c', status: 'Exists' }),
      item({ nodeId: 'd', status: 'Waiting' }),
      item({ nodeId: 'e', status: 'Incomplete' }),
    ]);

    expect(checkboxes().map((c) => c.checked)).toEqual([true, true, false, false, false]);
    expect(checkboxes().map((c) => c.disabled)).toEqual([false, false, true, true, true]);
    expect(text()).toContain('Zmienisz 2 rzeczy w „Domowy”');
    expect(text()).toContain('Teraz 400');
    expect(text()).toContain('To już jest');
    expect(text()).toContain('Czeka na miesiąc');
    expect(text()).toContain('Uzupełnij kafelek');
    expect(okButton().textContent).toContain('Zastosuj zaznaczone (2)');
  });

  it('odznaczenie akcji zmniejsza liczbę, a wysłanie niesie tylko zaznaczone identyfikatory', async () => {
    await open([item({ nodeId: 'a' }), item({ nodeId: 'b', type: 'CreateReservation', title: 'Wakacje' })]);

    checkboxes()[0].click();
    fixture.detectChanges();
    expect(okButton().textContent).toContain('(1)');

    okButton().click();
    const post = http.expectOne((r) => r.url === '/api/strategies/s1/apply' && r.method === 'POST');
    expect(post.request.body).toEqual({ nodeIds: ['b'] });
    post.flush({ applied: ['b'], skipped: [] });
    await fixture.whenStable();
  });

  it('dla wariantu pyta o jego podgląd, pokazuje nazwę i wysyła identyfikator wariantu', async () => {
    // Łapie stosowanie bazowego wariantu pod nazwą wybranego: bez variantId serwer policzyłby plan z wyłączonymi kafelkami.
    fixture.componentRef.setInput('variantId', 'v 1');
    fixture.componentRef.setInput('variantName', 'Bez celu');
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/strategies/s1/apply?variantId=v%201' && r.method === 'GET')
      .flush(preview([item({ nodeId: 'a' })]));
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(text()).toContain('Wariant: Bez celu');

    okButton().click();
    const post = http.expectOne((r) => r.url === '/api/strategies/s1/apply' && r.method === 'POST');
    expect(post.request.body).toEqual({ nodeIds: ['a'], variantId: 'v 1' });
    post.flush({ applied: ['a'], skipped: [] });
    await fixture.whenStable();
  });

  it('po zastosowaniu zgłasza zamknięcie okna', async () => {
    await open([item({ nodeId: 'a' })]);
    let closed = 0;
    fixture.componentInstance.closed.subscribe(() => closed++);

    okButton().click();
    http.expectOne((r) => r.method === 'POST').flush({ applied: ['a'], skipped: [] });
    await fixture.whenStable();

    expect(closed).toBe(1);
  });

  it('bez żadnej zaznaczonej akcji przycisk zastosowania jest wyłączony', async () => {
    await open([item({ nodeId: 'a', status: 'Exists' })]);

    expect(okButton().disabled).toBe(true);
    expect(checkboxes()[0].checked).toBe(false);
  });

  it('strategia bez akcji „do budżetu” pokazuje komunikat zamiast pustej listy', async () => {
    await open([]);

    expect(text()).toContain('Brak akcji.');
    expect(checkboxes()).toHaveLength(0);
  });
});
