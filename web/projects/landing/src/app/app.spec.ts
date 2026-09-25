import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { LANDING_ICONS } from './icons';
import { App } from './app';

/**
 * Landing page.
 *
 * <para>
 * ⚠️ Te testy NIE sprawdzają, jak strona wygląda — sprawdzają, czego OBIECUJE.
 * Kryteria akceptacji z issue #12 są w całości o uczciwości: żadnej funkcji z roadmapy
 * podanej jako istniejąca, żadnej obietnicy salda konta, obowiązkowa sekcja o granicach,
 * jedno prawdziwe CTA. To są rzeczy, które gniją po cichu — ktoś dopisze akapit
 * o prognozach albo zmieni CTA na „Wypróbuj”, wszystko dalej się zbuduje, a strona
 * zacznie kłamać. Kompilator tego nie złapie, więc łapie to ten plik.
 * </para>
 */
describe('Landing', () => {
  let fixture: ComponentFixture<App>;

  const text = (): string =>
    (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideNoopAnimations(), provideNzIcons(LANDING_ICONS)],
    }).compileComponents();

    fixture = TestBed.createComponent(App);
    fixture.detectChanges();
  });

  it('ma sekcję o granicach — nie samą listę zalet', () => {
    // Na prezentacji projektu ta sekcja buduje wiarygodność mocniej niż kolejna zaleta:
    // pokazuje, że granice są znane i wybrane, a nie przemilczane.
    expect(text()).toContain('Czego to nie robi');
    expect(text()).toContain('Nie łączy się z bankiem');
    expect(text()).toContain('Nie jest wielouserowe');
  });

  it('NIE obiecuje salda konta przy oszczędnościach', () => {
    // ⚠️ Kryterium akceptacji #12. Przed #10 `Transaction.AccountId` nie jest wypełniany
    // przy imporcie, więc „odłożone” liczy się z kategorii. Zdanie „pokazuje stan Twoich
    // oszczędności” byłoby obietnicą mocniejszą niż to, co robi kod.
    expect(text()).toContain('liczy się z kategorii, nie z salda konta');
    expect(text()).toContain('Nie zna salda Twojego konta');
  });

  it('każdy punkt roadmapy niesie status, a niezrobione NIE są opisane jako gotowe', () => {
    // ⚠️ Kryterium akceptacji #12 („funkcje z roadmapy wyłącznie w sekcji jawnie oznaczonej
    // jako plany”) jest tu spełnione statusem przy KAŻDYM punkcie, a nie jedną ramką wokół
    // wszystkich. To mocniejsze: żeby strona zaczęła kłamać, trzeba by skłamać punktowo.
    // Największa pokusa przy takiej stronie to prognozy i paragony — brzmią najlepiej
    // z całej listy i nie istnieją.
    const items = [...fixture.nativeElement.querySelectorAll('.road__item')] as HTMLElement[];
    expect(items.length).toBeGreaterThan(1);

    for (const item of items) {
      expect(item.querySelector('.road__status')?.textContent?.trim()).toBeTruthy();
    }

    const done = items.filter((i) => i.classList.contains('road__item--done'));
    expect(done.length).toBeGreaterThan(0);

    // ⚠️ Lista pilnuje wyłącznie tego, czego JESZCZE NIE MA. Wcześniej stały tu na sztywno
    // „dokładnie jeden punkt zrobiony" i „limity nie są gotowe" — obie asercje przeżyły
    // wdrożenie limitów oraz zleceń i przez to WYMUSZAŁY na stronie nieprawdę. Test ma
    // bronić uczciwości, a nie zamrażać stan sprzed roku: dopisując tu funkcję, dopisuj ją
    // dopiero wtedy, gdy naprawdę jej nie ma.
    const doneText = done.map((i) => i.textContent ?? '').join(' ');
    for (const feature of ['Prognozy', 'Paragony']) {
      const owner = items.find((i) => i.textContent?.includes(feature));
      expect(owner).toBeDefined();
      expect(owner!.classList.contains('road__item--done')).toBe(false);
      expect(owner!.querySelector('.road__status')!.textContent!.trim()).not.toBe('Gotowe');
      expect(doneText).not.toContain(feature);
    }
  });

  it('nie opisuje funkcji z roadmapy poza samą roadmapą', () => {
    // Bez tego ktoś dopisze akapit „a wkrótce prognozy” w sekcji o tym, co działa,
    // wszystko dalej się zbuduje, a strona zacznie obiecywać nieistniejący kod.
    // Tekst spoza roadmapy zbieramy z DOM-u, a nie odejmowaniem napisów — odejmowanie
    // wywracało się na białych znakach przy granicach elementów.
    const root = fixture.nativeElement as HTMLElement;
    const road = root.querySelector('.road') as HTMLElement;
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    let outside = '';
    for (let n = walker.nextNode(); n; n = walker.nextNode()) {
      if (!road.contains(n)) outside += ' ' + n.textContent;
    }

    for (const feature of ['Prognozy', 'Paragony']) {
      expect(outside).not.toContain(feature);
    }
  });

  it('nie zaprasza do wypróbowania czegoś, czego nie ma', () => {
    // Nie ma hostingu ani rejestracji, jest jedna baza z prywatnymi danymi. Strona, która
    // obiecuje coś, czego nie ma czym spełnić, szkodzi bardziej niż jej brak.
    expect(text()).not.toContain('Wypróbuj');
    expect(text()).not.toContain('Zarejestruj');
    expect(text()).not.toContain('Załóż konto');
  });

  it('każdy link wychodzący prowadzi ALBO do publicznego repo, ALBO do lokalnej aplikacji', () => {
    // ⚠️ Sprawdzamy KONKRETNE adresy, nie samo „github.com". Słabsza wersja tego warunku
    // przepuściła adres repozytorium PRYWATNEGO: wszystkie linki były w domenie github.com
    // i dawały 404 każdemu, kto nie jest autorem — czyli jedyne CTA nie prowadziło nigdzie.
    //
    // ⚠️ REWIZJA (2026-09-25): doszedł drugi dozwolony adres — lokalna instancja aplikacji.
    // Jest dopuszczalny WYŁĄCZNIE dopóki landing chodzi lokalnie. Gdy trafi na publiczny
    // hosting, `localhost` stanie się tym samym błędem co dawne prywatne repo, a ten test
    // ma wtedy paść i o tym przypomnieć.
    const allowed = ['https://github.com/KubaKrychowski/budget-tracker', 'https://localhost:4200'];
    const links = [...fixture.nativeElement.querySelectorAll('a[href^="http"]')] as HTMLAnchorElement[];

    expect(links.length).toBeGreaterThan(0);
    expect(links.every((a) => allowed.includes(a.getAttribute('href') ?? ''))).toBe(true);
  });

  it('ma przycisk prowadzący do aplikacji', () => {
    const toApp = [...fixture.nativeElement.querySelectorAll('a[href]')]
      .filter((a) => (a as HTMLAnchorElement).getAttribute('href') === 'https://localhost:4200');

    expect(toApp.length).toBeGreaterThan(0);
    expect(text()).toContain('Przejdź do aplikacji');
  });

  it('opisuje kolejkę do przeglądu razem z progiem, a nie samo „mądre AI”', () => {
    // Opis mechanizmu, który przyznaje się do błędów, jest sprawdzalny; bez tego jest reklamą.
    expect(text()).toContain('do przeglądu');
    expect(text()).toContain('0.7');
    expect(text()).toContain('Model się myli');
  });

  it('podaje 97% jako pomiar na własnych danych, a nie jako obietnicę', () => {
    // ⚠️ Reguły powstały z tego samego wyciągu, na którym je zmierzono. Bez tego zdania
    // liczba czyta się jak zapowiedź skuteczności na cudzym wyciągu.
    expect(text()).toContain('97%');
    expect(text()).toContain('na tych samych danych');
  });

  it('zrzuty mają opis, a znak graficzny go NIE ma', () => {
    // Dwie różne role, dwie różne reguły. Zrzut niesie treść, więc bez `alt` jest dla
    // czytnika ekranu pustym miejscem. Logo treści nie niesie — nazwa produktu stoi
    // obok jako tekst — więc `alt=""` jest tu POPRAWNE: opisane logo kazałoby czytnikowi
    // przeczytać tę samą nazwę dwa razy.
    const shots = [...fixture.nativeElement.querySelectorAll('.shot img')] as HTMLImageElement[];
    expect(shots.length).toBeGreaterThan(0);
    for (const img of shots) {
      expect(img.getAttribute('alt')).toBeTruthy();
    }

    const decorative = [...fixture.nativeElement.querySelectorAll('.bar__logo, .foot__logo')] as HTMLImageElement[];
    expect(decorative.length).toBeGreaterThan(0);
    for (const img of decorative) {
      expect(img.getAttribute('alt')).toBe('');
    }

    // Wymiary z góry na KAŻDYM obrazku — bez nich strona skacze przy wczytywaniu.
    const all = [...fixture.nativeElement.querySelectorAll('img')] as HTMLImageElement[];
    for (const img of all) {
      expect(img.getAttribute('width')).toBeTruthy();
      expect(img.getAttribute('height')).toBeTruthy();
    }
  });
});
