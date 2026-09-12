import { Resource, Signal, computed } from '@angular/core';

/**
 * Bezpieczny odczyt zasobu — jedyny sposób, w jaki wolno sięgać po `value()`.
 *
 * ⚠️ POWÓD ISTNIENIA TEJ FUNKCJI. `ResourceRef.value()` w stanie błędu NIE zwraca `undefined`,
 * tylko RZUCA („Resource is currently in an error state"). Zapis `this.list.value()?.items ?? []`
 * wygląda więc na odporny, a nie jest: przy odpowiedzi 400 rzuca w trakcie renderowania
 * szablonu. Render przerywa się w połowie, a ponieważ nie dochodzi do aktualizacji widoku,
 * `nz-spin` zostaje w stanie z poprzedniego przebiegu — czyli kręci się w nieskończoność,
 * nad pustą tabelą, bez śladu, że cokolwiek poszło nie tak.
 *
 * Objaw jest mylący (zawieszony spinner), a przyczyna jest o dwie warstwy dalej (rzucający
 * getter), więc pojedyncze `?.` w komponencie nie wystarczy — czytanie zasobu ma iść JEDNĄ
 * drogą, żeby nie dało się tego napisać źle przy kolejnym ekranie.
 */
export function valueOf<T>(resource: Resource<T | undefined>): Signal<T | undefined> {
  return computed(() => (resource.hasValue() ? resource.value() : undefined));
}

/**
 * Błąd zasobu jako sygnał — do pokazania na ekranie zamiast danych.
 *
 * Osobno od <see cref="valueOf" />, bo pusty wynik i wynik nieudany to dwa różne stany:
 * pierwszy znaczy „nic nie pasuje do filtra", drugi „nie wiadomo, co pasuje".
 * Sklejenie ich pokazywałoby użytkownikowi „brak danych" wtedy, gdy dane być może są.
 */
export function errorOf(resource: Resource<unknown>): Signal<Error | undefined> {
  return computed(() => (resource.status() === 'error' ? resource.error() : undefined));
}
