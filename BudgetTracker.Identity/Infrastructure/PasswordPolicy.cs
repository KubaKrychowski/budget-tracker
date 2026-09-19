namespace BudgetTracker.Identity.Infrastructure;

/// <summary>
/// Wymagania wobec hasła, które muszą się zgadzać w trzech miejscach: opcjach Identity (<c>Program.cs</c>),
/// walidacji formularzy i tekstach podpowiedzi. Jedna stała zamiast ósemki wpisanej w każdym z nich.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
}
