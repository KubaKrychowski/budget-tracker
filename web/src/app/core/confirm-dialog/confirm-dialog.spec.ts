import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService } from '@ngx-translate/core';
import { NZ_MODAL_DATA, NzModalRef } from 'ng-zorro-antd/modal';
import { ConfirmDialog } from './confirm-dialog';
import { ConfirmDialogOptions } from './confirm-dialog-options';

/**
 * Treść modala w izolacji — bez `NzModalService` (który otwierałby prawdziwy overlay).
 * `NZ_MODAL_DATA` i `NzModalRef` są tym, co serwis wstrzykuje w produkcji; tutaj podajemy
 * je ręcznie, żeby przetestować samą logikę blokady przycisku i to, co komponent
 * przekazuje przy zamknięciu.
 */
describe('ConfirmDialog', () => {
  let fixture: ComponentFixture<ConfirmDialog>;
  let component: ConfirmDialog;
  let closedWith: boolean | undefined;

  const api = () => component as unknown as {
    typedKey: { set(v: string): void };
    confirmDisabled(): boolean;
    confirm(): void;
    cancel(): void;
  };

  function create(data: ConfirmDialogOptions): void {
    closedWith = undefined;

    TestBed.configureTestingModule({
      imports: [ConfirmDialog],
      providers: [
        provideNoopAnimations(),
        provideTranslateService(),
        { provide: NZ_MODAL_DATA, useValue: data },
        {
          provide: NzModalRef,
          // Atrapa: jedyne, co komponent robi z `NzModalRef`, to `.close(value)` —
          // reszta API (afterClose, config…) nie jest mu do niczego potrzebna.
          useValue: { close: (value: boolean) => { closedWith = value; } },
        },
      ],
    });

    fixture = TestBed.createComponent(ConfirmDialog);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  afterEach(() => TestBed.resetTestingModule());

  it('bez confirmKey przycisk jest gotowy od razu', () => {
    create({ header: 'Czy na pewno?' });

    expect(api().confirmDisabled()).toBe(false);

    api().confirm();
    expect(closedWith).toBe(true);
  });

  it('z confirmKey blokuje potwierdzenie, dopóki wpisany tekst się nie zgadza', () => {
    create({ header: 'Usunąć budżet „Podstawowy"?', confirmKey: 'Podstawowy' });

    expect(api().confirmDisabled()).toBe(true);

    api().typedKey.set('Podstaw');
    expect(api().confirmDisabled()).toBe(true);

    // Klik przy zablokowanym przycisku nie ma prawa niczego zamknąć — to jest
    // ostatnia linia obrony, gdyby ktoś ominął `[disabled]` w szablonie.
    api().confirm();
    expect(closedWith).toBeUndefined();

    api().typedKey.set('Podstawowy');
    expect(api().confirmDisabled()).toBe(false);

    api().confirm();
    expect(closedWith).toBe(true);
  });

  it('klucz musi się zgadzać DOKŁADNIE — białe znaki na krawędziach są przycinane', () => {
    create({ header: 'Usunąć?', confirmKey: 'Testowy' });

    api().typedKey.set('  Testowy  ');
    expect(api().confirmDisabled()).toBe(false);

    api().typedKey.set('testowy'); // inna wielkość liter — to NIE jest ten sam klucz
    expect(api().confirmDisabled()).toBe(true);
  });

  it('operacja niszcząca ma czerwony przycisk potwierdzenia, zwykła nie', () => {
    const confirmButton = (): HTMLButtonElement =>
      fixture.nativeElement.querySelector('.confirm-dialog__actions button[nztype="primary"]');

    create({ header: 'Usunąć limit?', danger: true });
    expect(confirmButton().classList).toContain('button-danger');

    TestBed.resetTestingModule();
    create({ header: 'Cofnąć rozliczenie?' });
    expect(confirmButton().classList).not.toContain('button-danger');
  });

  it('anulowanie zamyka z `false`, niezależnie od stanu klucza', () => {
    create({ header: 'Usunąć?', confirmKey: 'Testowy' });

    api().cancel();
    expect(closedWith).toBe(false);
  });
});
