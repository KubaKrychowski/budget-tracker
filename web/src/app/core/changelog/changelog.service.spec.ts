import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NzModalService } from 'ng-zorro-antd/modal';
import { ChangelogRelease } from './changelog';
import { ChangelogService } from './changelog.service';

const release = (id: string): ChangelogRelease => ({ id, date: '2026-10-03', pl: [{ title: id, text: '' }], en: [] });
const RELEASES = [release('2.1'), release('2.0.3'), release('2.0.2')];

describe('ChangelogService', () => {
  function setup(seen?: string) {
    localStorage.clear();
    if (seen) localStorage.setItem(ChangelogService.SeenKey, seen);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: NzModalService, useValue: {} }],
    });
    const service = TestBed.inject(ChangelogService);
    service.load();
    TestBed.inject(HttpTestingController).expectOne('changelog.json').flush(RELEASES);
    return service;
  }

  afterEach(() => localStorage.clear());

  it('pierwsza wizyta pokazuje tylko najnowsze wydanie', () => {
    const service = setup();
    expect(service.unseen().map((r) => r.id)).toEqual(['2.1']);
    expect(service.hasUnread()).toBe(true);
  });

  it('pokazuje wydania nowsze niż ostatnio widziane', () => {
    expect(setup('2.0.2').unseen().map((r) => r.id)).toEqual(['2.1', '2.0.3']);
  });

  it('nic nie jest nowe, gdy widziano najnowsze', () => {
    expect(setup('2.1').hasUnread()).toBe(false);
  });

  it('nieznane zapamiętane wydanie zachowuje się jak pierwsza wizyta', () => {
    expect(setup('0.1').unseen().map((r) => r.id)).toEqual(['2.1']);
  });

  it('markSeen zapamiętuje najnowsze wydanie i gasi kropkę', () => {
    const service = setup();
    service.markSeen();
    expect(localStorage.getItem(ChangelogService.SeenKey)).toBe('2.1');
    expect(service.hasUnread()).toBe(false);
  });

  it('błąd pobrania zostawia pustą listę i nic nie jest nowe', () => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: NzModalService, useValue: {} }],
    });
    const service = TestBed.inject(ChangelogService);
    service.load();
    TestBed.inject(HttpTestingController).expectOne('changelog.json').flush('x', { status: 404, statusText: 'Not Found' });
    expect(service.releases()).toEqual([]);
    expect(service.hasUnread()).toBe(false);
  });
});
