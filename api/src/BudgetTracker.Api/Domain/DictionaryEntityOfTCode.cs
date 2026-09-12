using BudgetTracker.Api.Domain.Consts;

namespace BudgetTracker.Api.Domain;

/// <summary>
/// Słownik z czytelnym <see cref="Code"/> jako kluczem głównym. Kolumny odwołujące się do słownika
/// przechowują ten sam kod, więc przeglądając bazę nie trzeba sprawdzać, co znaczy liczba.
/// </summary>
/// <typeparam name="TCode">
/// <c>string</c> dla słowników będących danymi (waluty) albo enum dla słowników, na których opiera się logika
/// (statusy). Enum trafia do bazy jako nazwa.
/// </typeparam>
/// <remarks>
/// ⚠️ Kod jest generyczny, a nie zawsze <c>string</c>, bo EF zakłada klucz obcy wyłącznie między właściwościami
/// tego samego typu CLR. Kolumna <c>Transaction.Status</c> jest typu <see cref="TransactionStatus"/> — słownik
/// z kluczem <c>string</c> dałby się połączyć z nią tylko ręcznym SQL-em poza modelem, niewidocznym dla migracji
/// i testów. Zmiana nazwy członka enuma jest dozwolona, ale wymaga migracji danych.
/// </remarks>
public abstract class DictionaryEntity<TCode>(TCode code) : DictionaryEntity
    where TCode : notnull
{
    public TCode Code { get; protected set; } = code;
}
