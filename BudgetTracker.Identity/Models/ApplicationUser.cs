using Microsoft.AspNetCore.Identity;

namespace BudgetTracker.Identity.Models;

/// <summary>Konto użytkownika Wydatki.coma — jeden użytkownik może być właścicielem wielu budżetów w API.</summary>
public class ApplicationUser : IdentityUser<Guid>;
