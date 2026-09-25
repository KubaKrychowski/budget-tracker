import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { APP_ICONS } from '../../core/icons';
import { TerminalService } from '../../core/terminal/terminal.service';
import { Functions } from './functions';

/** Katalog „Wszystkie funkcje" — grupowanie, filtr na miejscu i dokąd prowadzą wiersze. */
describe('Functions', () => {
  let fixture: ComponentFixture<Functions>;

  const text = (): string => (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');

  const rowByName = (name: string): HTMLButtonElement =>
    [...fixture.nativeElement.querySelectorAll('button')]
      .find((b) => (b as HTMLElement).textContent?.includes(name)) as HTMLButtonElement;

  const filterTo = (value: string): void => {
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Functions],
      providers: [
        provideRouter([
          { path: 'dashboard', children: [] },
          { path: 'limits', children: [] },
          { path: 'settings', children: [] },
        ]),
        provideNoopAnimations(),
        provideTranslateService(),
        provideNzIcons(APP_ICONS),
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('pl', {
      dashboard: { breadcrumb: 'Dashboard' },
      actions: {
        addTransaction: 'Dodaj transakcje',
        createBudget: 'Utwórz budżet',
        transactionList: 'Lista transakcji',
        reviewQueue: 'Popraw kategorie',
        importStatement: 'Import wyciągu',
        savings: 'Cele oszczędzania',
        limits: 'Limity wydatków',
        standingOrders: 'Zlecenia stałe',
        episodicOrders: 'Zlecenia epizodyczne',
        dashboard: 'Dashboard',
        reservations: 'Rezerwacje',
        categoryRules: 'Reguły kategoryzacji',
        modelTraining: 'Trening modelu',
        handbook: 'Podręcznik',
        allFunctions: 'Wszystkie funkcje',
      },
      functions: {
        title: 'Wszystkie funkcje',
        filter: 'Filtruj funkcje',
        filterHint: 'Filtr zawęża katalog na miejscu.',
        empty: 'Żadna funkcja nie pasuje do „{{query}}”.',
        groups: {
          daily: 'Pieniądze na co dzień',
          planning: 'Planowanie',
          budgets: 'Budżety',
          settings: 'Ustawienia i narzędzia',
        },
        terminal: { name: 'Terminal CLI', description: 'Te same komendy co moduł „bt”' },
        descriptions: {
          limits: 'Limit na kategorię i historia zmian',
          'model-training': 'Zbiór uczący, wersje i aktywacja',
        },
      },
    });
    translate.use('pl');

    fixture = TestBed.createComponent(Functions);
    fixture.detectChanges();
  });

  it('pokazuje wszystkie cztery grupy w stałej kolejności', () => {
    const titles = [...fixture.nativeElement.querySelectorAll('.fun__group-title')]
      .map((h) => (h as HTMLElement).textContent?.trim());

    expect(titles).toEqual(['Pieniądze na co dzień', 'Planowanie', 'Budżety', 'Ustawienia i narzędzia']);
  });

  it('NIE wymienia sam siebie — wpis prowadzący na ten sam ekran byłby ślepą pętlą', () => {
    expect(text()).not.toContain('Wszystkie funkcje —');
    expect(rowByName('Wszystkie funkcje')).toBeUndefined();
  });

  it('niesie ekrany spoza kafli, których wcześniej nie było w rejestrze', () => {
    expect(text()).toContain('Rezerwacje');
    expect(text()).toContain('Reguły kategoryzacji');
    expect(text()).toContain('Trening modelu');
    expect(text()).toContain('Podręcznik');
  });

  it('pod nazwą stoi opis, co na tym ekranie zrobisz', () => {
    expect(text()).toContain('Limit na kategorię i historia zmian');
  });

  it('filtr zawęża katalog i chowa puste grupy', () => {
    filterTo('limit');

    expect(text()).toContain('Limity wydatków');
    expect(text()).not.toContain('Import wyciągu');
    // Grupa bez trafień znika w całości, zamiast zostawiać pusty nagłówek.
    expect(text()).not.toContain('Budżety');
  });

  it('filtr działa bez polskich ogonków i po opisie, nie tylko po nazwie', () => {
    filterTo('uczacy');

    expect(text()).toContain('Trening modelu');
  });

  it('brak trafień mówi, czego szukano', () => {
    filterTo('zupelnie nic takiego');

    expect(text()).toContain('Żadna funkcja nie pasuje do „zupelnie nic takiego”.');
  });

  it('wiersz z zakładką ustawień niesie parametr adresu, nie ląduje na domyślnej', async () => {
    const router = TestBed.inject(Router);
    rowByName('Trening modelu').click();
    await new Promise((resolve) => setTimeout(resolve));

    expect(router.url).toContain('/settings');
    expect(router.parseUrl(router.url).queryParams['tab']).toBe('training');
  });

  it('akcja bez trasy jest wygaszona, zamiast udawać nawigację', () => {
    // „Dodaj transakcje" nie ma jeszcze ekranu — patrz SystemAction.route.
    expect(rowByName('Dodaj transakcje').disabled).toBe(true);
  });

  it('Terminal otwiera panel, bo jest panelem, a nie trasą', () => {
    const terminal = TestBed.inject(TerminalService);
    expect(terminal.isOpen()).toBe(false);

    rowByName('Terminal CLI').click();

    expect(terminal.isOpen()).toBe(true);
  });
});
