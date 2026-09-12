namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>Wynik podglądu reguły: ile transakcji by złapała i kilka przykładów.</summary>
/// <remarks>
/// <para>
/// ⚠️ <c>MatchCount</c> to liczba trafień SAMEJ tej reguły, a nie liczba transakcji, które dostałyby
/// od niej kategorię. Wygrywa pierwsza pasująca reguła (niższy priorytet = wcześniej), więc część
/// tych transakcji może już być łapana przez regułę stojącą wyżej. Dlatego jest osobne
/// <c>ShadowedCount</c> — bez niego podgląd obiecywałby skutek, którego reguła nie ma.
/// </para>
/// <para>
/// <c>ScannedCount</c> mówi, na ilu transakcjach liczono. Bez tego „0 trafień" jest nieczytelne:
/// nie wiadomo, czy wzorzec jest zły, czy po prostu nie ma jeszcze danych.
/// </para>
/// </remarks>
public sealed record RulePreviewResponseDto(
    int MatchCount,
    int ShadowedCount,
    int ScannedCount,
    IReadOnlyList<RulePreviewMatchResponseDto> Samples);
