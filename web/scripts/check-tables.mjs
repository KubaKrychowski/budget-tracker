// Strażnik konwencji tabel, uruchamiany w `npm run lint`.
//
// ⚠️ `[nzShowPagination]="false"` NIE wyłącza stronicowania w `nz-table` — chowa tylko przełącznik
// stron, a tabela dalej pokazuje 10 wierszy (domyślne `nzPageSize`). Wiersze powyżej dziesiątego
// stają się nieosiągalne i nic tego nie sygnalizuje. Tak wyglądało pięć tabel naraz: reguły
// (138 z 148 niewidocznych), miesiące oszczędności (2 z 12), kategorie w danych treningowych
// (~18 z ~28), wersje modelu i rezerwacje.
//
// Wyjątek: paginacja po stronie SERWERA (`[nzFrontPagination]="false"`) — dane przychodzą już
// pocięte, więc chowanie kontrolki niczego nie ukrywa.
//
// Skrypt, a nie spec: specy kompilują się bez typów Node, a dokładanie `@types/node` tylko dla
// tego sprawdzenia byłoby nową zależnością bez innego pożytku.
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';

const roots = ['src', 'projects'];

function templates(dir) {
  let entries;
  try { entries = readdirSync(dir); } catch { return []; }
  return entries.flatMap((name) => {
    const path = join(dir, name);
    if (name === 'node_modules' || name === 'dist') return [];
    if (statSync(path).isDirectory()) return templates(path);
    return name.endsWith('.html') ? [path] : [];
  });
}

const tables = [];
for (const file of roots.flatMap(templates)) {
  const html = readFileSync(file, 'utf8').replace(/<!--[\s\S]*?-->/g, '');
  for (const tag of html.match(/<nz-table\b[^>]*>/g) ?? []) tables.push({ file, tag });
}

// Bez tego strażnik przechodziłby po cichu, gdyby kiedyś przestał znajdować szablony.
if (tables.length === 0) {
  console.error('check-tables: nie znaleziono ani jednej <nz-table> — strażnik nie sprawdza niczego.');
  process.exit(1);
}

const offenders = tables.filter(({ tag }) =>
  /\[nzShowPagination\]="false"/.test(tag) && !/\[nzFrontPagination\]="false"/.test(tag));

if (offenders.length > 0) {
  console.error('check-tables: tabele chowają stronicowanie, które nadal tnie wiersze do 10:');
  for (const { file } of offenders) console.error('  - ' + relative(process.cwd(), file));
  console.error('Usuń [nzShowPagination]="false" i ustaw [nzPageSize] (albo przejdź na paginację serwerową).');
  process.exit(1);
}

console.log(`check-tables: ${tables.length} tabel, żadna nie chowa stronicowania.`);
