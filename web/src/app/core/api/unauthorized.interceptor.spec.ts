import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Observable, Subject, of, throwError } from 'rxjs';
import { unauthorizedInterceptor } from './unauthorized.interceptor';

describe('unauthorizedInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let refresh: () => Observable<{ isAuthenticated: boolean }>;
  let refreshCalls: number;
  let authorize: ReturnType<typeof vi.fn>;
  let logoffLocal: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    refreshCalls = 0;
    authorize = vi.fn();
    logoffLocal = vi.fn();
    refresh = () => of({ isAuthenticated: true });

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([unauthorizedInterceptor])),
        provideHttpClientTesting(),
        {
          provide: OidcSecurityService,
          useValue: {
            forceRefreshSession: () => { refreshCalls++; return refresh(); },
            authorize,
            logoffLocal,
          },
        },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  const unauthorized = { status: 401, statusText: 'Unauthorized' };

  it('po 401 odświeża sesję i ponawia żądanie RAZ', () => {
    let result: unknown;
    http.get('/api/budgets').subscribe((r) => (result = r));

    controller.expectOne('/api/budgets').flush(null, unauthorized);
    controller.expectOne('/api/budgets').flush({ ok: true });

    expect(result).toEqual({ ok: true });
    expect(refreshCalls).toBe(1);
    expect(authorize).not.toHaveBeenCalled();
  });

  it('gdy odświeżenie się nie udało, odsyła na logowanie i oddaje błąd', () => {
    refresh = () => of({ isAuthenticated: false });
    let status = 0;
    http.get('/api/budgets').subscribe({ error: (e) => (status = e.status) });

    controller.expectOne('/api/budgets').flush(null, unauthorized);

    expect(status).toBe(401);
    expect(logoffLocal).toHaveBeenCalledTimes(1);
    expect(authorize).toHaveBeenCalledTimes(1);
  });

  it('błąd odświeżenia (np. timeout ramki) traktuje jak nieudane odświeżenie', () => {
    refresh = () => throwError(() => new Error('timeout'));
    http.get('/api/budgets').subscribe({ error: () => undefined });

    controller.expectOne('/api/budgets').flush(null, unauthorized);

    expect(authorize).toHaveBeenCalledTimes(1);
  });

  it('drugi 401 na ponowionym żądaniu nie zapętla odświeżania — idzie na logowanie', () => {
    http.get('/api/budgets').subscribe({ error: () => undefined });

    controller.expectOne('/api/budgets').flush(null, unauthorized);
    controller.expectOne('/api/budgets').flush(null, unauthorized);

    expect(refreshCalls).toBe(1);
    expect(authorize).toHaveBeenCalledTimes(1);
  });

  it('równoległe 401 dzielą jedno odświeżenie i jedno przekierowanie', () => {
    const gate = new Subject<{ isAuthenticated: boolean }>();
    refresh = () => gate;
    http.get('/api/a').subscribe({ error: () => undefined });
    http.get('/api/b').subscribe({ error: () => undefined });

    controller.expectOne('/api/a').flush(null, unauthorized);
    controller.expectOne('/api/b').flush(null, unauthorized);
    gate.next({ isAuthenticated: false });
    gate.complete();

    expect(refreshCalls).toBe(1);
    expect(authorize).toHaveBeenCalledTimes(1);
  });

  it('innych błędów niż 401 nie rusza', () => {
    let status = 0;
    http.get('/api/budgets').subscribe({ error: (e) => (status = e.status) });

    controller.expectOne('/api/budgets').flush(null, { status: 500, statusText: 'Server Error' });

    expect(status).toBe(500);
    expect(refreshCalls).toBe(0);
  });

  it('401 spoza /api/ nie ma nic wspólnego z sesją', () => {
    http.get('/i18n/pl.json').subscribe({ error: () => undefined });

    controller.expectOne('/i18n/pl.json').flush(null, unauthorized);

    expect(refreshCalls).toBe(0);
    expect(authorize).not.toHaveBeenCalled();
  });
});
