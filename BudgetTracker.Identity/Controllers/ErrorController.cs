using System.Diagnostics;
using BudgetTracker.Identity.Models;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Identity.Controllers;

/// <summary>Strona błędu dla nieobsłużonych wyjątków poza Development (<c>UseExceptionHandler</c> w <c>Program.cs</c>).</summary>
public sealed class ErrorController : Controller
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
