// Strażnik odnawiania sesji, uruchamiany w `npm run lint`.
//
// ⚠️ Odnawianie sesji (silent renew) ładuje `silent-renew.html` w UKRYTEJ RAMCE. Static Web Apps doklejało jednak
// `X-Frame-Options: DENY` i `frame-ancestors 'none'` (globalHeaders) do KAŻDEGO pliku, także tego. Przeglądarka odmawiała
// wtedy wyrenderowania strony w ramce, odnowienie nigdy się nie kończyło (tylko timeout ok. 20 s), a ekrany w tym czasie
// dostawały 401 z wygasłym tokenem — po długiej sesji trzeba było przeładować okno (zgłoszenie #32). Nic tego nie
// sygnalizowało: test jednostkowy nie widzi nagłówków hostingu.
//
// Skrypt, a nie spec: specy kompilują się bez typów Node (patrz `check-tables.mjs`).
import { readFileSync } from 'node:fs';

const config = JSON.parse(readFileSync('public/staticwebapp.config.json', 'utf8'));
const html = readFileSync('public/silent-renew.html', 'utf8');
const problems = [];

const route = (config.routes ?? []).find((r) => r.route === '/silent-renew.html');
const csp = route?.headers?.['Content-Security-Policy'] ?? '';

if (!route) problems.push('brak wpisu routes dla /silent-renew.html — dziedziczy globalne DENY i ramka nigdy się nie załaduje');
if (route && route.headers?.['X-Frame-Options'] !== 'SAMEORIGIN') problems.push('X-Frame-Options tej strony musi być SAMEORIGIN');
if (route && !/frame-ancestors 'self'(;|$)/.test(csp)) problems.push("CSP tej strony musi zawierać frame-ancestors 'self'");
if (route && /frame-ancestors \*|frame-ancestors https?:/.test(csp)) problems.push('frame-ancestors tej strony nie może wpuszczać obcych originów');

// Reszta serwisu zostaje nie do osadzenia — wyjątek dotyczy WYŁĄCZNIE tej jednej strony.
if (config.globalHeaders?.['X-Frame-Options'] !== 'DENY') problems.push('globalne X-Frame-Options musi zostać DENY');
if (!/frame-ancestors 'none'/.test(config.globalHeaders?.['Content-Security-Policy'] ?? '')) {
  problems.push("globalne CSP musi zostać z frame-ancestors 'none'");
}

// Skrypt inline wymagałby w CSP hasha zależnego od końców linii — dlatego skrypt jest w osobnym pliku.
if (/<script(?![^>]*\bsrc=)[^>]*>/i.test(html)) problems.push('silent-renew.html ma skrypt inline — CSP tej strony dopuszcza tylko script-src \'self\'');
if (!/script-src 'self'/.test(csp)) problems.push("CSP tej strony musi dopuszczać script-src 'self' (skrypt jest w silent-renew.js)");

if (problems.length > 0) {
  console.error(`check-silent-renew: ${problems.length} problem(ów):\n- ${problems.join('\n- ')}`);
  process.exit(1);
}
console.log('check-silent-renew: strona odnawiania sesji da się osadzić w ramce, reszta serwisu nie.');
