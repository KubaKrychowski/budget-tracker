import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { apiBaseUrlInterceptor } from './api-base-url.interceptor';
import { setRuntimeConfigForTests } from '../runtime-config';

/**
 * Front i API stoją na Azure pod RÓŻNYMI adresami (Static Web Apps kontra App Service), bo plan Free
 * Static Web Apps nie ma „linked backend". Te testy pilnują, żeby adres API dokładał się dokładnie tam,
 * gdzie trzeba — pomyłka tutaj to aplikacja, która po wdrożeniu nie wykonuje ani jednego żądania.
 */
describe('apiBaseUrlInterceptor', () => {
  const setup = (apiBaseUrl: string) => {
    setRuntimeConfigForTests({ identityAuthority: 'https://identity.example', apiBaseUrl });

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiBaseUrlInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    return {
      http: TestBed.inject(HttpClient),
      httpMock: TestBed.inject(HttpTestingController),
    };
  };

  afterEach(() => {
    setRuntimeConfigForTests({ identityAuthority: 'https://localhost:7226', apiBaseUrl: '' });
  });

  it('dokleja adres API do żądań pisanych względnie', () => {
    const { http, httpMock } = setup('https://api.example');

    http.get('/api/budgets').subscribe();

    httpMock.expectOne('https://api.example/api/budgets').flush({});
    httpMock.verify();
  });

  it('nie rusza adresu, gdy adres API jest pusty', () => {
    // Tak działa dev: `ng serve` ma proxy, więc żądanie ma zostać względne.
    const { http, httpMock } = setup('');

    http.get('/api/budgets').subscribe();

    httpMock.expectOne('/api/budgets').flush({});
    httpMock.verify();
  });

  it('nie rusza żądań spoza /api', () => {
    // Tłumaczenia (`/i18n/pl.json`) i podręcznik serwuje ten sam host co aplikację — doklejenie im
    // adresu API wysłałoby je do backendu, który ich nie ma.
    const { http, httpMock } = setup('https://api.example');

    http.get('/i18n/pl.json').subscribe();

    httpMock.expectOne('/i18n/pl.json').flush({});
    httpMock.verify();
  });

  it('nie robi podwójnego ukośnika, gdy adres API kończy się ukośnikiem', () => {
    const { http, httpMock } = setup('https://api.example/');

    http.get('/api/budgets').subscribe();

    httpMock.expectOne('https://api.example/api/budgets').flush({});
    httpMock.verify();
  });
});
