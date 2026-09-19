namespace BudgetTracker.Api.Features.Admin.Consts;

/// <summary>Nazwy polityk autoryzacji modułu administracyjnego.</summary>
public static class AdminPolicies
{
    /// <summary>
    /// Wymaga zakresu <c>budgettracker_admin</c> w tokenie. Dostaje go wyłącznie klient serwisowy serwera
    /// tożsamości (client credentials), nigdy SPA ani <c>bt-cli</c> — użytkownik nie ma jak go zdobyć, także jako admin.
    /// </summary>
    public const string Admin = "AdminApi";
}
