using Microsoft.ML;

namespace BudgetTracker.Api.Features.Categorization.Models;

/// <summary>Aktywny model użytkownika, gotowy do utworzenia silnika predykcji.</summary>
/// <remarks>
/// <para>
/// <see cref="ETag"/> jest TOŻSAMOŚCIĄ tej wersji — nadaje go blob przy zapisie i zmienia się przy
/// każdej publikacji. Służy jako klucz pamięci podręcznej wszędzie tam, gdzie coś jest liczone „na model”.
/// </para>
/// <para>
/// ⚠️ Trzymamy <see cref="ITransformer"/>, a NIE <c>PredictionEngine</c>: transformer wolno współdzielić
/// między wątkami, silnik nie. Dzięki temu model pobiera się raz na wersję, a każde żądanie robi sobie
/// własny, krótkożyjący silnik — bez blokady i bez pytania, kto go zwalnia.
/// </para>
/// </remarks>
public sealed record ActiveCategoryModel(string ETag, MLContext Ml, ITransformer Model);
