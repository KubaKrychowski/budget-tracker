import { rememberPageSize, rememberedPageSize } from './page-size-memory';

describe('page-size-memory', () => {
  const allowed = [10, 20, 50, 100];

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('bez wpisu nic nie jest zapamiętane', () => {
    expect(rememberedPageSize('transactions', allowed)).toBeNull();
  });

  it('zwraca to, co zapisano', () => {
    rememberPageSize('transactions', 50);

    expect(rememberedPageSize('transactions', allowed)).toBe(50);
  });

  it('każda tabela ma własny wpis', () => {
    rememberPageSize('transactions', 50);

    expect(rememberedPageSize('inne', allowed)).toBeNull();
  });

  it('wartość spoza dozwolonych jest ignorowana, żeby tabela nie dostała rozmiaru, którego nie umie pokazać', () => {
    rememberPageSize('transactions', 37);

    expect(rememberedPageSize('transactions', allowed)).toBeNull();
  });

  it('uszkodzony wpis to „nic nie zapamiętano”, nie wyjątek', () => {
    localStorage.setItem('bt.pageSize.transactions', 'abc');

    expect(rememberedPageSize('transactions', allowed)).toBeNull();
  });

  it('klucz ma prefiks „bt.”, który czyści wylogowanie', () => {
    rememberPageSize('transactions', 20);

    // Łapie zmianę klucza na taki, którego `clearLocalAppState` nie czyści — cudzy rozmiar zostałby po zmianie konta.
    expect(localStorage.getItem('bt.pageSize.transactions')).toBe('20');
  });
});
