namespace BudgetTracker.Api.Features.Categorization.Contracts;

/// <summary>Ekran „Dane treningowe": skład zbioru, rozkład po kategoriach i wersje modelu.</summary>
/// <param name="CorrectionsSinceLastTraining">
/// Ile poprawek przybyło od ostatniego treningu. To jedyna liczba na tym ekranie, która ma
/// kogoś do czegoś SKŁONIĆ — retrening jest świadomie ręczny (plan, punkt 7), więc ekran
/// zachęca liczbą zamiast odpalać się sam.
/// </param>
/// <param name="MetricsAreIndicative">
/// Zawsze <c>true</c> — pole istnieje po to, żeby front nie musiał wpisywać tego założenia
/// u siebie. <c>TrainTestSplit</c> ma ustalone ziarno, ale dzieli INNY zbiór przy każdym
/// treningu, bo zbiór rośnie. 0,91 po 0,89 nie znaczy „lepszy model", tylko „inny podział".
/// </param>
public sealed record TrainingSetOverviewResponseDto(
    TrainingSetCompositionResponseDto Composition,
    IReadOnlyList<CategoryExampleCountResponseDto> Categories,
    int PendingReview,
    int CorrectionsSinceLastTraining,
    IReadOnlyList<ModelVersionResponseDto> Models,
    bool MetricsAreIndicative = true);
