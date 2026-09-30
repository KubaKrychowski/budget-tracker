namespace BudgetTracker.Api.Features.Budgets.Services;

/// <summary>Jedna transakcja na wejściu księgi: to, co potrzebne do bilansu, bez reszty encji.</summary>
/// <param name="Id">Kolejność zapisu — rozstrzyga tylko tam, gdzie saldo z banku nie pozwala ustalić kolejności.</param>
/// <param name="BalanceAfter">Saldo po operacji z wyciągu; <c>null</c> = brak (transakcja ręczna, bank go nie podał).</param>
public readonly record struct LedgerInput(int Id, DateOnly Date, decimal Amount, decimal? BalanceAfter);

/// <summary>Transakcja w księdze: kwota i bilans budżetu PO niej.</summary>
public readonly record struct LedgerEntry(DateOnly Date, decimal Amount, decimal Balance);

/// <summary>
/// Bilans budżetu odczytywany z salda, które podał bank, zamiast liczony jako „bilans początkowy + suma kwot".
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>⚠️ <b>Po co.</b> Wzór „początkowy + suma" jest poprawny tylko wtedy, gdy w bazie jest KAŻDA transakcja od punktu startu
/// i punkt startu jest dokładnie znany. Po imporcie plików z różnych okresów rozjeżdża się z bankiem o dowolną kwotę, a nic
/// w aplikacji nie wskazuje dlaczego. Saldo z wyciągu jest tym, co bank naprawdę pokazał, więc dashboard ma się z nim zgadzać.</item>
/// <item><b>Reguła:</b> transakcja z saldem USTAWIA bilans na to saldo; transakcja bez salda (ręczna) DOLICZA swoją kwotę do bilansu
/// z poprzedniej pozycji. Bilans początkowy budżetu jest punktem wyjścia wyłącznie wtedy, gdy ŻADNA transakcja nie ma salda —
/// wtedy wynik jest identyczny jak ze starego wzoru.</item>
/// <item>⚠️ <b>Kolejność w obrębie dnia.</b> Kolejność zapisu (<see cref="LedgerInput.Id"/>) nie zawsze zgadza się z kolejnością
/// w banku (plik importowany w częściach, kolejność operacji po stronie banku), więc „ostatnia transakcja dnia" według <c>Id</c>
/// bywa nieprawdziwa i dashboard pokazałby saldo ze środka dnia. Dlatego wiersze dnia układamy tak, żeby każde saldo = poprzednie
/// + kwota (<see cref="OrderByChain"/>). Gdy się nie da (brakuje wiersza, saldo niespójne), zostaje kolejność zapisu.</item>
/// <item>Przed pierwszą transakcją z saldem bilans jest jej saldem sprzed operacji (saldo minus kwota) — to stan konta, który znamy
/// z banku; bilans początkowy budżetu nie jest tu potrzebny.</item>
/// </list>
/// </remarks>
public sealed class BalanceLedger
{
    /// <summary>Tolerancja porównania sald — grosz. Kwoty mają 2 miejsca, więc mniejsza różnica to zaokrąglenie, nie inna liczba.</summary>
    private const decimal Tolerance = 0.005m;

    /// <summary>Górny limit kroków szukania kolejności w dniu — chroni przed wybuchem, gdy saldo dnia jest niespójne.</summary>
    private const int MaxSearchSteps = 20_000;

    private BalanceLedger(decimal opening, IReadOnlyList<LedgerEntry> entries, bool usesBankBalances)
    {
        Opening = opening;
        Entries = entries;
        UsesBankBalances = usesBankBalances;
    }

    /// <summary>
    /// Czy bilans jest odczytywany z sald banku (jakakolwiek transakcja ma saldo). Wtedy bilans początkowy budżetu niczego nie zmienia —
    /// ekran edycji budżetu musi to powiedzieć, zamiast obiecywać, że jego zmiana „przeliczy cały budżet".
    /// </summary>
    public bool UsesBankBalances { get; }

    /// <summary>Bilans przed pierwszą transakcją księgi.</summary>
    public decimal Opening { get; }

    /// <summary>Transakcje w kolejności chronologicznej (data, potem ułożenie wg salda), każda z bilansem po niej.</summary>
    public IReadOnlyList<LedgerEntry> Entries { get; }

    /// <summary>Bilans po ostatniej transakcji; bez transakcji — <see cref="Opening"/>.</summary>
    public decimal Closing => Entries.Count == 0 ? Opening : Entries[^1].Balance;

    /// <summary>Bilans na początku dnia <paramref name="date"/>, czyli po ostatniej transakcji ze WCZEŚNIEJSZEJ daty.</summary>
    public decimal BalanceBefore(DateOnly date)
    {
        var balance = Opening;
        foreach (var entry in Entries)
        {
            if (entry.Date >= date) break;
            balance = entry.Balance;
        }
        return balance;
    }

    /// <summary>Buduje księgę z transakcji jednego budżetu, podanych w dowolnej kolejności.</summary>
    /// <param name="initialBalance">Bilans początkowy budżetu — używany tylko wtedy, gdy żadna transakcja nie ma salda.</param>
    public static BalanceLedger Build(decimal initialBalance, IEnumerable<LedgerInput> rows)
    {
        var days = new List<(DateOnly Date, List<LedgerInput> Rows)>();
        decimal? reference = null;

        foreach (var day in rows.OrderBy(r => r.Date).ThenBy(r => r.Id).GroupBy(r => r.Date))
        {
            var chained = OrderByChain(day.Where(r => r.BalanceAfter is not null).ToList(), reference);
            if (chained.Count > 0) reference = chained[^1].BalanceAfter;

            days.Add((day.Key, [.. chained, .. day.Where(r => r.BalanceAfter is null)]));
        }

        var opening = initialBalance;
        var firstWithBalance = days.SelectMany(d => d.Rows).FirstOrDefault(r => r.BalanceAfter is not null);
        if (firstWithBalance.BalanceAfter is { } balance) opening = balance - firstWithBalance.Amount;

        var entries = new List<LedgerEntry>();
        var running = opening;
        foreach (var (date, dayRows) in days)
        {
            foreach (var row in dayRows)
            {
                running = row.BalanceAfter ?? running + row.Amount;
                entries.Add(new LedgerEntry(date, row.Amount, running));
            }
        }

        return new BalanceLedger(opening, entries, usesBankBalances: firstWithBalance.BalanceAfter is not null);
    }

    /// <summary>
    /// Układa wiersze z saldem z JEDNEGO dnia tak, żeby każde saldo było poprzednim plus kwota. Pierwszy wiersz zaczyna od
    /// <paramref name="reference"/> (saldo zamykające poprzedni dzień), a gdy go nie ma albo nie da się od niego ułożyć całego dnia —
    /// od dowolnego wiersza, który daje pełny łańcuch. Gdy łańcuch się nie składa, zwraca kolejność zapisu.
    /// </summary>
    internal static List<LedgerInput> OrderByChain(IReadOnlyList<LedgerInput> bankRows, decimal? reference)
    {
        var rows = bankRows.OrderBy(r => r.Id).ToList();
        if (rows.Count <= 1) return rows;

        if (reference is { } start && TryChain(rows, start) is { } fromReference) return fromReference;
        if (TryChain(rows, null) is { } fromAnyStart) return fromAnyStart;

        return rows;
    }

    private static List<LedgerInput>? TryChain(List<LedgerInput> rows, decimal? start)
    {
        var used = new bool[rows.Count];
        var path = new List<LedgerInput>(rows.Count);
        var steps = 0;

        bool Extend(decimal? current)
        {
            if (path.Count == rows.Count) return true;
            if (++steps > MaxSearchSteps) return false;

            for (var i = 0; i < rows.Count; i++)
            {
                if (used[i]) continue;

                var before = rows[i].BalanceAfter!.Value - rows[i].Amount;
                if (current is { } expected && Math.Abs(before - expected) >= Tolerance) continue;

                used[i] = true;
                path.Add(rows[i]);
                if (Extend(rows[i].BalanceAfter)) return true;

                path.RemoveAt(path.Count - 1);
                used[i] = false;
            }

            return false;
        }

        return Extend(start) ? path : null;
    }
}
