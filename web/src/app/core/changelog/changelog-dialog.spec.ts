import { registerLocaleData } from '@angular/common';
import pl from '@angular/common/locales/pl';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { NZ_MODAL_DATA, NzModalRef } from 'ng-zorro-antd/modal';
import { ChangelogRelease } from './changelog';
import { ChangelogDialog, ChangelogDialogData } from './changelog-dialog';

registerLocaleData(pl);

const rel = (id: string, link?: string): ChangelogRelease => ({
  id,
  date: '2026-10-03',
  pl: [{ title: `PL ${id}`, text: 't', link, linkLabel: 'Otwórz' }],
  en: [{ title: `EN ${id}`, text: 't' }],
});

describe('ChangelogDialog', () => {
  const close = vi.fn();

  function create(data: ChangelogDialogData, lang = 'pl') {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideTranslateService(),
        { provide: NZ_MODAL_DATA, useValue: data },
        { provide: NzModalRef, useValue: { close } },
      ],
    });
    TestBed.inject(TranslateService).use(lang);
    const fixture = TestBed.createComponent(ChangelogDialog);
    fixture.detectChanges();
    return fixture;
  }
  const titles = (f: ReturnType<typeof create>) =>
    Array.from((f.nativeElement as HTMLElement).querySelectorAll('.cl__item-title')).map((e) => e.textContent);
  const buttons = (f: ReturnType<typeof create>) =>
    Array.from((f.nativeElement as HTMLElement).querySelectorAll('.cl__footer button')).map((e) => e.textContent?.trim());

  beforeEach(() => close.mockClear());

  it('po aktualizacji pokazuje tylko nowe wydania, „Rozumiem" i „Pokaż wszystkie" (klucze i18n)', () => {
    const f = create({ releases: [rel('2.1'), rel('2.0')], newIds: ['2.1'], auto: true });
    expect(titles(f)).toEqual(['PL 2.1']);
    expect(buttons(f)).toEqual(['changelog.showAll', 'changelog.ok']);
    expect((f.nativeElement as HTMLElement).querySelector('.cl__badge')).toBeTruthy();
  });

  it('„Pokaż wszystkie zmiany" rozwija historię i zamienia przyciski na „Zamknij"', () => {
    const f = create({ releases: [rel('2.1'), rel('2.0')], newIds: ['2.1'], auto: true });
    (f.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.cl__all')!.click();
    f.detectChanges();
    expect(titles(f)).toEqual(['PL 2.1', 'PL 2.0']);
    expect(buttons(f)).toEqual(['changelog.close']);
  });

  it('historia z ikony od razu pokazuje wszystko, plakietka tylko przy nowych', () => {
    const f = create({ releases: [rel('2.1'), rel('2.0')], newIds: [], auto: false });
    expect(titles(f)).toHaveLength(2);
    expect((f.nativeElement as HTMLElement).querySelector('.cl__badge')).toBeNull();
  });

  it('wybiera treść w języku interfejsu', () => {
    const f = create({ releases: [rel('2.1')], newIds: [], auto: false }, 'en');
    expect(titles(f)).toEqual(['EN 2.1']);
  });

  it('link do ekranu zamyka okno i nawiguje', () => {
    const f = create({ releases: [rel('2.1', '/strategies')], newIds: ['2.1'], auto: true });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    (f.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('.cl__link')!.click();
    expect(close).toHaveBeenCalled();
    expect(navigate).toHaveBeenCalledWith('/strategies');
  });
});
