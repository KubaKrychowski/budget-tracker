import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { provideNzDateFnsAdapter } from 'ng-zorro-antd/core/time';
import { APP_ICONS } from '../../../core/icons';
import { ReceiptCandidate, ReceiptRead } from '../../../core/api/models/receipts';
import { TransactionListItem } from '../../../core/api/models/transaction-list-item';
import { ReceiptDialog } from './receipt-dialog';

/** Gospodarz z sygnałami — dialog ma tylko wejścia i wyjścia, więc test steruje nim jak rodzic. */
@Component({
  imports: [ReceiptDialog],
  template: `<app-receipt-dialog
    [open]="open()" [transaction]="transaction()" [budgetIds]="['b1']"
    (closed)="closed = closed + 1" (saved)="saved = saved + 1" />`,
})
class Host {
  readonly open = signal(true);
  readonly transaction = signal<TransactionListItem | null>(null);
  closed = 0;
  saved = 0;
}

/**
 * Dodawanie paragonu: wgranie → ekran weryfikacji → zapis. Sprawdzamy to, co łatwo zepsuć po cichu:
 * że nic nie łączy się samo bez wyboru, że kandydaci odświeżają się po zmianie sumy i że anulowanie po odczycie
 * kasuje wgrany plik. Teksty to klucze tłumaczeń (w teście nie ma słownika).
 */
describe('ReceiptDialog', () => {
  let fixture: ComponentFixture<Host>;
  let host: Host;
  let http: HttpTestingController;

  const uploaded = (over: Partial<ReceiptRead> = {}): ReceiptRead => ({
    id: 'r1',
    fileName: 'paragon.jpg',
    merchant: 'Sklep Przykład',
    date: '2026-10-08',
    total: 20.6,
    merchantUncertain: false,
    dateUncertain: false,
    totalUncertain: true,
    ...over,
  });

  const exact: ReceiptCandidate = {
    id: 't1', date: '2026-10-08', description: 'ZAKUP KARTA', amount: -20.6, match: 'Exact',
  };

  const text = (): string => document.body.textContent ?? '';

  /** Bez `whenStable`: przy nieobsłużonym żądaniu HTTP wisi w nieskończoność (patrz web/CLAUDE.md). */
  const settle = async (): Promise<void> => {
    for (let i = 0; i < 4; i++) {
      fixture.detectChanges();
      await new Promise((resolve) => setTimeout(resolve, 0));
    }
  };

  /** Wgrywa plik przez ten sam punkt wejścia co `nz-upload` i odpowiada na odczyt. */
  const uploadFile = async (body: ReceiptRead, type = 'image/jpeg', size = 1000): Promise<void> => {
    const dialog = fixture.debugElement.children[0].componentInstance as unknown as {
      captureFile(file: unknown): boolean;
    };
    const file = new File([new Uint8Array(size)], 'paragon.jpg', { type });
    dialog.captureFile(file);
    await settle();
    http.match((r) => r.url === '/api/receipts' && r.method === 'POST').forEach((r) => r.flush(body));
    await settle();
  };

  const candidateRequests = () =>
    http.match((r) => r.url === '/api/receipts/candidates');

  beforeEach(async () => {
    // Środowisko testowe nie ma adresów blob — przeglądarka ma, więc kod produkcyjny nie jest tu osłaniany.
    URL.createObjectURL = () => 'blob:test';
    URL.revokeObjectURL = () => undefined;

    await TestBed.configureTestingModule({
      imports: [Host],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideTranslateService(),
        provideNzIcons(APP_ICONS),
        provideNzI18n(pl_PL),
        provideNzDateFnsAdapter(),
      ],
    }).compileComponents();

    TestBed.inject(TranslateService).use('pl');
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Host);
    host = fixture.componentInstance;
    await settle();
  });

  afterEach(() => {
    http.match(() => true).forEach((r) => { if (!r.cancelled) r.flush([]); });
    document.querySelectorAll('.ant-modal-root').forEach((n) => n.remove());
  });

  it('na starcie pokazuje wybór pliku i informację o wysyłce do chmury', () => {
    expect(text()).toContain('transactions.receipt.drop');
    expect(text()).toContain('transactions.receipt.privacy');
  });

  it('odrzuca za duży plik po stronie przeglądarki, bez wysyłania go', async () => {
    const dialog = fixture.debugElement.children[0].componentInstance as unknown as { captureFile(f: unknown): boolean };
    dialog.captureFile(new File([new Uint8Array(11 * 1024 * 1024)], 'duzy.jpg', { type: 'image/jpeg' }));
    await settle();

    // Łapie błąd, w którym płatny odczyt leciał na pliku, który serwer i tak odrzuci.
    expect(http.match((r) => r.url === '/api/receipts').length).toBe(0);
    expect(text()).toContain('transactions.receipt.tooLarge');
  });

  it('po odczycie pokazuje pola, oznacza niepewną sumę i najlepszego kandydata zaznacza z góry', async () => {
    await uploadFile(uploaded());
    candidateRequests().forEach((r) => r.flush([exact]));
    await settle();

    expect(text()).toContain('transactions.receipt.reviewTitle');
    expect(text()).toContain('transactions.receipt.check');
    expect((document.querySelector('#rcpt-merchant') as HTMLInputElement).value).toBe('Sklep Przykład');
    expect((document.querySelector('#rcpt-c-t1') as HTMLInputElement).checked).toBe(true);
  });

  it('zapis wysyła poprawione pola i wybraną transakcję, a potem zgłasza zapis', async () => {
    await uploadFile(uploaded());
    candidateRequests().forEach((r) => r.flush([exact]));
    await settle();

    const ok = Array.from(document.querySelectorAll('.ant-modal-footer button'))
      .find((b) => (b.textContent ?? '').includes('transactions.receipt.attach')) as HTMLButtonElement;
    ok.click();
    await settle();

    const put = http.expectOne((r) => r.url === '/api/receipts/r1' && r.method === 'PUT');
    expect(put.request.body).toMatchObject({ transactionId: 't1', merchant: 'Sklep Przykład', date: '2026-10-08', total: 20.6 });
    put.flush({});
    await settle();

    expect(host.saved).toBe(1);
    expect(host.closed).toBe(1);
  });

  it('bez wybranej transakcji przycisk zapisu jest wygaszony — nic nie łączy się samo', async () => {
    await uploadFile(uploaded());
    candidateRequests().forEach((r) => r.flush([{ ...exact, match: 'Possible' } as ReceiptCandidate]));
    await settle();

    const buttons = document.querySelectorAll('.ant-modal-footer button');
    const ok = buttons[buttons.length - 1] as HTMLButtonElement;
    // „Możliwy” kandydat nie jest zaznaczany z góry — to decyzja człowieka.
    expect(ok.disabled).toBe(true);
  });

  it('zmiana sumy odświeża kandydatów', async () => {
    await uploadFile(uploaded());
    candidateRequests().forEach((r) => r.flush([exact]));
    await settle();

    (fixture.debugElement.children[0].componentInstance as unknown as { setTotal(v: number): void }).setTotal(35);
    await settle();

    const requests = candidateRequests();
    expect(requests.map((r) => r.request.params.get('total'))).toContain('35');
    requests.forEach((r) => r.flush([]));
  });

  it('z menu wiersza nie pyta o transakcję i wysyła ją z góry', async () => {
    host.transaction.set({
      id: 't9', date: '2026-10-08', description: 'ZAKUP KARTA', amount: -20.6, categoryId: null, categoryName: null,
      status: 'Imported', episodicOrderId: null, episodicOrderName: null, confidence: null,
      savingsTransferBudgetId: null, balanceAfter: null, receiptCount: 0,
    });
    await settle();
    await uploadFile(uploaded());

    expect(document.querySelector('.rcpt__candidates')).toBeNull();
    expect(candidateRequests().length).toBe(0);

    const ok = Array.from(document.querySelectorAll('.ant-modal-footer button'))
      .find((b) => (b.textContent ?? '').includes('transactions.receipt.attach')) as HTMLButtonElement;
    ok.click();
    await settle();

    const put = http.expectOne((r) => r.url === '/api/receipts/r1' && r.method === 'PUT');
    expect((put.request.body as { transactionId: string }).transactionId).toBe('t9');
    put.flush({});
  });

  it('anulowanie po odczycie kasuje wgrany paragon', async () => {
    await uploadFile(uploaded());
    candidateRequests().forEach((r) => r.flush([]));
    await settle();

    const cancel = Array.from(document.querySelectorAll('.ant-modal-footer button'))
      .find((b) => (b.textContent ?? '').includes('transactions.receipt.cancel')) as HTMLButtonElement;
    cancel.click();
    await settle();

    // Łapie plik zostawiony w magazynie bez właściciela w UI.
    const del = http.expectOne((r) => r.url === '/api/receipts/r1' && r.method === 'DELETE');
    del.flush(null);
    await settle();
    expect(host.closed).toBe(1);
  });
});
