using Microsoft.ML.Data;

namespace BudgetTracker.Api.Features.Categorization.Models;

/// <summary>
/// Które sloty słowne modelu mogą otworzyć bramkę słownika — czyli czy opis mówi coś o SPRZEDAWCY.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ „Znane słowo" to za mało (zgłoszenie #9, druga odsłona). Parser PKO dokleja do każdej płatności
/// kartą „Miasto: …", a „miasto" zapalało się w zbiorze w 19 z 25 kategorii. Bramka oparta na samej
/// znajomości słowa przepuszczała więc KAŻDĄ płatność kartą, także losowy ciąg znaków — model dawał
/// wtedy „Subskrypcje" z pewnością 0,72–0,81, czyli powyżej progu.
/// </para>
/// <para>
/// <see cref="Open"/> to stan „bramka nie blokuje niczego": używany, gdy bloku słownego nie da się wskazać
/// albo gdy nie ma z czego policzyć rozlania. Bramka jest siatką bezpieczeństwa, a nie warunkiem
/// poprawności — jej brak przywraca zachowanie sprzed jej wprowadzenia, zamiast zatrzymywać kategoryzację.
/// </para>
/// </remarks>
public sealed class VocabularyGateSlots(int wordSlotStart, bool[]? informative)
{
    /// <summary>Bramka przepuszczająca wszystko.</summary>
    public static VocabularyGateSlots Open { get; } = new(-1, null);

    /// <summary>Indeks pierwszego slotu bloku słownego; -1 = nie udało się go znaleźć.</summary>
    public int WordSlotStart { get; } = wordSlotStart;

    /// <summary>Czy opis zapalił choć jeden INFORMATYWNY slot słowny.</summary>
    /// <remarks>
    /// Celowo blok słowny, nie znakowy: char-gramy zapalają się prawie zawsze, bo trójka liter w rodzaju
    /// „ent" trafia się w czymkolwiek — także w losowym ciągu. Blok słowny jest zero-jedynkowy, więc nie
    /// ma tu progu do strojenia: albo model zna jakieś słowo z tego opisu, albo nie zna żadnego.
    ///
    /// Slot, którego zbiór treningowy nie zapalił, liczy się jak dotąd — wykluczamy wyłącznie sloty
    /// z UDOWODNIONYM rozlaniem, żeby bramka nie wysyłała po cichu do przeglądu wszystkiego, czego
    /// akurat nie potrafi policzyć.
    /// </remarks>
    public bool Allows(in VBuffer<float> descriptionFeatures)
    {
        if (WordSlotStart < 0) return true;

        foreach (var slot in LitWordSlots(descriptionFeatures, WordSlotStart))
        {
            if (informative is null || slot >= informative.Length || informative[slot]) return true;
        }

        return false;
    }

    /// <summary>Indeksy zapalonych slotów bloku słownego.</summary>
    public static IEnumerable<int> LitWordSlots(VBuffer<float> features, int wordSlotStart)
    {
        var values = features.GetValues().ToArray();

        if (features.IsDense)
        {
            for (var i = wordSlotStart; i < values.Length; i++)
                if (values[i] != 0)
                    yield return i;

            yield break;
        }

        var indices = features.GetIndices().ToArray();
        for (var i = 0; i < indices.Length; i++)
            if (indices[i] >= wordSlotStart && values[i] != 0)
                yield return indices[i];
    }
}
