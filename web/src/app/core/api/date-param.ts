/**
 * Backend przyjmuje DateOnly, więc wysyłamy czystą datę bez czasu i strefy.
 * toISOString() jest tu pułapką: konwertuje do UTC i przy dodatnim offsecie
 * potrafi cofnąć dzień, przez co dashboard pokazałby zły zakres.
 */
export function toIsoDate(d: Date): string {
  const month = `${d.getMonth() + 1}`.padStart(2, '0');
  const day = `${d.getDate()}`.padStart(2, '0');
  return `${d.getFullYear()}-${month}-${day}`;
}

/**
 * Odwrotność toIsoDate. Świadomie NIE `new Date(s)` — ten konstruktor parsuje
 * "YYYY-MM-DD" jako północ UTC, więc przy dodatnim offsecie strefy cofa dzień
 * (dokładnie ta sama pułapka, co przy toISOString() opisana wyżej).
 */
export function fromIsoDate(s: string): Date {
  const [year, month, day] = s.split('-').map(Number);
  return new Date(year, month - 1, day);
}
