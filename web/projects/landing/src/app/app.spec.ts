import { ComponentFixture, TestBed } from '@angular/core/testing';
import { App } from './app';

/**
 * Landing page.
 *
 * ⚠️ Te testy NIE sprawdzają, jak strona wygląda — sprawdzają, czego OBIECUJE i jakim językiem mówi.
 * To rzeczy, które gniją po cichu: ktoś dopisze akapit o prognozach, zmieni CTA na „Wypróbuj” albo wklei
 * z powrotem żargon, wszystko dalej się zbuduje, a strona zacznie kłamać albo przestanie być zrozumiała
 * dla zwykłego człowieka. Kompilator tego nie złapie, więc łapie to ten plik.
 */
describe('Landing', () => {
  let fixture: ComponentFixture<App>;

  const text = (): string =>
    (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
    }).compileComponents();

    fixture = TestBed.createComponent(App);
    fixture.detectChanges();
  });

  it('ma sekcję o granicach — nie samą listę zalet', () => {
    expect(text()).toContain('Czego jeszcze nie ma');
    expect(text()).toContain('Sam nie łączy się z bankiem');
    expect(text()).toContain('To wczesna wersja');
    expect(text()).toContain('Nie czyta paragonów');
  });

  it('nie obiecuje funkcji, których nie ma', () => {
    // Bez tego ktoś dopisze „wkrótce prognozy” albo „łączy się z Twoim bankiem”, wszystko dalej się zbuduje,
    // a strona zacznie obiecywać nieistniejący kod.
    for (const promise of ['Prognoz', 'automatycznie z bankiem', 'połączysz konto', 'Paragony i pozycje']) {
      expect(text()).not.toContain(promise);
    }
  });

  it('mówi prostym językiem — bez żargonu programistycznego', () => {
    // Uwaga z oglądu strony przez osobę z zewnątrz: „bardzo techniczna”. Żargon ma zostać w repozytorium,
    // a strona ma być zrozumiała dla kogoś, kto nigdy nie widział kodu.
    for (const jargon of ['ML.NET', 'RLS', 'ADR', 'Angular', 'PostgreSQL', 'model ML', 'API', 'parser', 'CLAUDE.md']) {
      expect(text()).not.toContain(jargon);
    }
  });

  it('nie zaprasza do wypróbowania czegoś, czego nie ma', () => {
    // Konto założysz tylko z zaproszeniem. Strona, która obiecuje coś, czego nie da się od razu zrobić,
    // szkodzi bardziej niż jej brak.
    expect(text()).not.toContain('Wypróbuj');
    expect(text()).not.toContain('Zarejestruj');
    expect(text()).not.toContain('Załóż konto');
  });

  it('każdy link wychodzący prowadzi do publicznego repo, aplikacji, jej wydań albo profilu autora', () => {
    // ⚠️ Sprawdzamy KONKRETNE adresy, nie samo „github.com". Słabsza wersja przepuściła kiedyś adres
    // repozytorium PRYWATNEGO — link dawał 404 każdemu, kto nie jest autorem. Lista zostaje zamknięta.
    const allowed = [
      'https://github.com/KubaKrychowski/budget-tracker',
      'https://app.wydatki.com',
      'https://www.linkedin.com/in/kuba-krychowski/',
      'https://github.com/KubaKrychowski/budget-tracker/releases',
      'https://github.com/KubaKrychowski/budget-tracker/releases/latest/download/wydatki.apk',
    ];
    const links = [...fixture.nativeElement.querySelectorAll('a[href^="http"]')] as HTMLAnchorElement[];

    expect(links.length).toBeGreaterThan(0);
    expect(links.every((a) => allowed.includes(a.getAttribute('href') ?? ''))).toBe(true);
  });

  it('aplikację na Androida pobiera się z NAJNOWSZEGO wydania, a iPhone dostaje wersję webową', () => {
    // ⚠️ Stały adres `releases/latest/download/wydatki.apk` — link do konkretnej wersji zestarzałby się po
    // pierwszym nowym wydaniu, a workflow release-android.yml zawsze nazywa plik wydatki.apk.
    const section = fixture.nativeElement.querySelector('#aplikacja') as HTMLElement;
    const hrefs = [...section.querySelectorAll('a[href]')].map((a: Element) => a.getAttribute('href'));

    expect(hrefs).toContain('https://github.com/KubaKrychowski/budget-tracker/releases/latest/download/wydatki.apk');
    expect(section.textContent).toContain('Pobierz na Androida');
    expect(section.textContent).toContain('iPhone');
    expect(hrefs).toContain('https://app.wydatki.com');

    const nav = [...fixture.nativeElement.querySelectorAll('.bar__nav a')].map((a: Element) => a.getAttribute('href'));
    expect(nav).toContain('#aplikacja');
  });

  it('osoba z zaproszeniem ma jak się zalogować, a główne wezwanie to prośba o dostęp', () => {
    const hrefs = [...fixture.nativeElement.querySelectorAll('a[href]')].map((a: Element) => a.getAttribute('href'));

    expect(hrefs).toContain('https://app.wydatki.com');
    expect(text()).toContain('Zaloguj się');
    expect(hrefs).toContain('#beta');
    expect(text()).toContain('Poproś o dostęp do bety');
  });

  it('opisuje prywatność wprost i linkuje do polityki', () => {
    expect(text()).toContain('Pliku z banku nie zapisuję');
    expect(text()).toContain('Nikomu nie sprzedaję i nie udostępniam danych');
    const hrefs = [...fixture.nativeElement.querySelectorAll('a[href]')].map((a: Element) => a.getAttribute('href'));
    expect(hrefs).toContain('/polityka-prywatnosci');
  });

  it('zrzuty mają opis, a znak graficzny jest TYLKO w górnym pasku i go nie ma', () => {
    // Zrzut niesie treść, więc bez `alt` jest dla czytnika ekranu pustym miejscem. Logo treści nie niesie —
    // nazwa stoi obok jako tekst — więc `alt=""` jest POPRAWNE. Logo ma być wyłącznie w górnym pasku.
    const shots = [...fixture.nativeElement.querySelectorAll('.shot img')] as HTMLImageElement[];
    expect(shots.length).toBeGreaterThan(0);
    for (const img of shots) {
      expect(img.getAttribute('alt')).toBeTruthy();
    }

    const logos = [...fixture.nativeElement.querySelectorAll('img[src$="logo.svg"]')] as HTMLImageElement[];
    expect(logos.length).toBe(1);
    expect(logos[0].closest('.bar')).not.toBeNull();
    expect(logos[0].getAttribute('alt')).toBe('');

    // Wymiary z góry na KAŻDYM obrazku — bez nich strona skacze przy wczytywaniu.
    const all = [...fixture.nativeElement.querySelectorAll('img')] as HTMLImageElement[];
    for (const img of all) {
      expect(img.getAttribute('width')).toBeTruthy();
      expect(img.getAttribute('height')).toBeTruthy();
    }
  });

  it('mówi, kto za tym stoi, i nie zmyśla przy tym życiorysu', () => {
    expect(text()).toContain('Kto za tym stoi');

    // ⚠️ Powód powstania ma pozostać TYM, który podał autor. To jedyne zdanie na stronie, którego nie da się
    // sprawdzić w kodzie, więc łatwo o podmianę na marketingowy ogólnik.
    expect(text()).toContain('gdzie w środku miesiąca znikają mi pieniądze');

    const links = [...fixture.nativeElement.querySelectorAll('a[href]')].map((a: Element) => a.getAttribute('href'));
    expect(links).toContain('https://www.linkedin.com/in/kuba-krychowski/');
  });

  // ── Prośba o dostęp do bety ────────────────────────────────────────────────────────────

  describe('prośba o dostęp do bety', () => {
    const query = <T extends Element>(selector: string): T => fixture.nativeElement.querySelector(selector) as T;

    const type = (selector: string, value: string): void => {
      const input = query<HTMLInputElement>(selector);
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };

    const submit = async (): Promise<void> => {
      query<HTMLFormElement>('.beta__form').dispatchEvent(new Event('submit'));
      await fixture.whenStable();
      fixture.detectChanges();
    };

    const acceptConsent = (): void => {
      const box = query<HTMLInputElement>('#beta-consent');
      box.checked = true;
      box.dispatchEvent(new Event('change'));
    };

    let fetchMock: ReturnType<typeof vi.fn>;

    beforeEach(() => {
      fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 202 });
      vi.stubGlobal('fetch', fetchMock);
    });

    afterEach(() => vi.unstubAllGlobals());

    it('bez zgody i z błędnym adresem niczego nie wysyła i mówi, co poprawić', async () => {
      // ⚠️ Zgoda to podstawa prawna zapisu adresu — formularz bez niej nie może wyjść z przeglądarki.
      type('#beta-email', 'to-nie-jest-adres');
      await submit();

      expect(fetchMock).not.toHaveBeenCalled();
      expect(text()).toContain('Podaj poprawny adres e-mail.');
      expect(text()).toContain('Zgoda jest wymagana, żeby zapisać adres.');
    });

    it('poprawny adres ze zgodą idzie na serwer tożsamości i pokazuje potwierdzenie', async () => {
      type('#beta-email', '  jan@example.com ');
      acceptConsent();
      await submit();

      expect(fetchMock).toHaveBeenCalledTimes(1);
      const [url, init] = fetchMock.mock.calls[0];
      expect(url).toMatch(/\/api\/beta-requests$/);
      expect(init.method).toBe('POST');
      expect(JSON.parse(init.body)).toEqual({ email: 'jan@example.com', consent: true, website: '' });
      expect(text()).toContain('Zapisane. Dziękuję!');
      expect(query('.beta__form')).toBeNull();
    });

    it('wypełnione pole-pułapka jedzie na serwer, który sam rozpozna automat', async () => {
      type('#beta-email', 'bot@example.com');
      type('#beta-website', 'http://spam');
      acceptConsent();
      await submit();

      expect(JSON.parse(fetchMock.mock.calls[0][1].body).website).toBe('http://spam');
    });

    it('przy limicie żądań (429) mówi o zbyt wielu próbach i zostawia formularz', async () => {
      fetchMock.mockResolvedValue({ ok: false, status: 429 });
      type('#beta-email', 'jan@example.com');
      acceptConsent();
      await submit();

      expect(text()).toContain('Za dużo prób');
      expect(query('.beta__form')).not.toBeNull();
    });

    it('przy braku połączenia pokazuje błąd zamiast udawać sukces', async () => {
      fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));
      type('#beta-email', 'jan@example.com');
      acceptConsent();
      await submit();

      expect(text()).toContain('Nie udało się zapisać adresu');
      expect(text()).not.toContain('Zapisane. Dziękuję!');
    });

    it('zgoda linkuje do regulaminu i polityki prywatności, a stopka do wszystkich dokumentów', () => {
      const links = [...fixture.nativeElement.querySelectorAll('a[href]')].map((a: Element) => a.getAttribute('href'));

      for (const path of ['/regulamin', '/polityka-prywatnosci', '/usun-konto', '/kontakt']) {
        expect(links).toContain(path);
      }
    });
  });
});
