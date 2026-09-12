namespace BudgetTracker.Api.Features.Transactions.Contracts;

/// <summary>
/// Kafle „Podsumowanie" nad tabelą, policzone dla zestawu WYNIKAJĄCEGO Z FILTRÓW TABELI.
/// </summary>
/// <remarks>
/// Stronicowanie jest tu celowo pominięte: strona to tylko okno na te same dane, więc kafle
/// odpowiadają na „ile tego jest w tym, co wyfiltrowałem", a nie „ile jest na wierszach
/// 11–20". Dlatego liczy je serwer — klient trzyma w ręku jedną stronę i sam by nie umiał.
///
/// Wielkości są WĘŻSZE niż na dashboardzie i to celowo. „Najdroższa kategoria" pod filtrem
/// kategorii tylko powtarza filtr, a bez niego i tak stoi na wykresie dashboardu.
/// </remarks>
/// <param name="Balance">
/// Saldo (suma ze znakiem) = <see cref="TotalIncome"/> − <see cref="TotalExpenses"/>.
/// Wyprowadzone z tych dwóch, nie liczone osobnym zapytaniem — inaczej liczba pod tabelą
/// mogłaby nie zgadzać się z kaflami nad nią. NIE ma własnego kafla: mieszanie wydatków
/// z przychodami w jedną liczbę nic nie mówiło, a licznik pod tabelą już ją pokazuje.
/// </param>
/// <param name="LargestExpenseAmount">Jako wartość dodatnia — tak, jak prezentuje ją UI.</param>
public sealed record TransactionSummaryResponseDto(
    decimal TotalExpenses,
    decimal TotalIncome,
    decimal Balance,
    decimal LargestExpenseAmount)
{
    public static readonly TransactionSummaryResponseDto Empty = new(0m, 0m, 0m, 0m);
}
