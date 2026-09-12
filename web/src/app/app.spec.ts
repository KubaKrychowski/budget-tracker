import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideTranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { APP_ICONS } from './core/icons';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        // App renderuje <router-outlet />, więc potrzebuje routera w teście.
        provideRouter([]),
        provideNzIcons(APP_ICONS),
        // Bez loadera — test nie sięga po pliki i18n, a TranslatePipe zwraca klucze.
        // Sprawdzamy strukturę shella, nie treść tłumaczeń.
        provideTranslateService(),
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render the router outlet shell', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
  });
});
