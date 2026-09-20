using BudgetTracker.Api.Domain;
using BudgetTracker.Api.Features.Admin.Exceptions;
using BudgetTracker.Api.Features.Budgets;
using BudgetTracker.Api.Features.Budgets.Exceptions;
using BudgetTracker.Api.Features.Categorization.Exceptions;
using BudgetTracker.Api.Features.Limits.Exceptions;
using BudgetTracker.Api.Features.Savings.Exceptions;
using BudgetTracker.Api.Features.EpisodicOrders.Exceptions;
using BudgetTracker.Api.Features.StandingOrders.Exceptions;
using BudgetTracker.Api.Features.Transactions.Exceptions;
using BudgetTracker.Api.Infrastructure.Exceptions;
using BudgetTracker.Api.Resources;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Localization;

namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Zamienia wyjątki domenowe na odpowiedzi HTTP — jedno miejsce zamiast <c>try/catch</c>
/// powtórzonego przy każdym endpoincie.
///
/// <para>
/// Powód: reguła „nieznany <c>BusinessId</c> = 404" jest przekrojowa. Rozpisana przy każdej
/// operacji z osobna prędzej czy później gdzieś wypadnie — i wtedy literówka w adresie da
/// <b>500</b> zamiast 404, czyli awarię zamiast normalnej odpowiedzi na złe wejście.
/// </para>
///
/// <para>
/// ⚠️ Cena tego rozwiązania: kody odpowiedzi przestają być widoczne przy endpoincie.
/// Dlatego endpointy deklarują je przez <c>Produces</c> — to jedyne, co zostaje w kodzie
/// jako ślad po kontrakcie.
/// </para>
///
/// <para>
/// ⚠️ Handler obsługuje TYLKO wyjątki wymienione w <see cref="Map"/>. Wszystko inne przepuszcza
/// dalej (<c>false</c>), żeby prawdziwa awaria nie została po cichu zamieniona w ładny 4xx.
/// </para>
/// </summary>
public sealed class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>
    /// Wyjątek → kod HTTP i klucz zasobu z komunikatem. <c>null</c> jako klucz znaczy
    /// „odpowiedź bez treści" — przy 404 nie ma czego napisać poza samym kodem.
    /// </summary>
    private static (int Status, string? ResourceKey)? Map(Exception exception) => exception switch
    {
        // Żądanie jest poprawne, to stan zasobu na nie nie pozwala — stąd 409, nie 400.
        BudgetDisabledException => (StatusCodes.Status409Conflict, "Import_BudgetDisabled"),
        // Reguła wspólna (bazowa) jest tylko do odczytu — żądanie poprawne, stan zasobu nie pozwala.
        CategoryRuleSharedReadOnlyException => (StatusCodes.Status409Conflict, "CategoryRule_SharedReadOnly"),
        // Ta sama zasada: trening jest poprawnym żądaniem, tylko nie w trakcie importu.
        TrainingBusyException => (StatusCodes.Status409Conflict, "Training_Busy"),
        TrainingDataMissingException => (StatusCodes.Status400BadRequest, "Training_NoData"),
        ModelVersionNotFoundException => (StatusCodes.Status404NotFound, "Training_UnknownModel"),
        BudgetNameRequiredException => (StatusCodes.Status400BadRequest, "Budget_NameRequired"),
        SavingsLinkTargetInvalidException => (StatusCodes.Status400BadRequest, "SavingsTransfer_LinkTargetInvalid"),
        SavingsTransferRulesInvalidException => (StatusCodes.Status400BadRequest, "SavingsTransfer_RulesInvalid"),
        SavingsTransferPatternInvalidException => (StatusCodes.Status400BadRequest, "SavingsTransfer_PatternInvalid"),
        SavingsTransferAmountInvalidException => (StatusCodes.Status400BadRequest, "SavingsTransfer_AmountInvalid"),
        TransactionDescriptionRequiredException =>
            (StatusCodes.Status400BadRequest, "Transaction_DescriptionRequired"),
        TransactionDescriptionTooLongException =>
            (StatusCodes.Status400BadRequest, "Transaction_DescriptionTooLong"),
        SavingsGoalAmountInvalidException =>
            (StatusCodes.Status400BadRequest, "Savings_AmountInvalid"),
        // Rezygnacja z celu, którego nie ma — 404, a nie ciche „ok": użytkownik ma się dowiedzieć,
        // że patrzy na nieaktualny ekran, zamiast myśleć, że coś wyłączył.
        SavingsGoalNotFoundException => (StatusCodes.Status404NotFound, null),

        ReservationNameRequiredException =>
            (StatusCodes.Status400BadRequest, "Reservation_NameRequired"),
        ReservationAmountInvalidException =>
            (StatusCodes.Status400BadRequest, "Reservation_AmountInvalid"),
        // 400, nie 404: identyfikator transakcji przychodzi w CIELE żądania, a nie w adresie
        // zasobu — 404 mówiłby „nie ma takiego endpointu". Ta sama zasada co przy imporcie.
        SettlementTransactionInvalidException =>
            (StatusCodes.Status400BadRequest, "Reservation_SettlementInvalid"),
        // Żądanie jest poprawne, to stan zasobu na nie nie pozwala — stąd 409.
        ReservationAlreadySettledException =>
            (StatusCodes.Status409Conflict, "Reservation_AlreadySettled"),
        ContributionAmountInvalidException => (StatusCodes.Status400BadRequest, "Contribution_AmountInvalid"),
        ContributionExceedsBalanceException => (StatusCodes.Status400BadRequest, "Contribution_ExceedsBalance"),
        ReservationAmountBelowContributedException => (StatusCodes.Status400BadRequest, "Reservation_AmountBelowContributed"),

        LimitAmountInvalidException => (StatusCodes.Status400BadRequest, "Limit_AmountInvalid"),
        LimitWarningThresholdInvalidException =>
            (StatusCodes.Status400BadRequest, "Limit_WarningThresholdInvalid"),
        // 400, nie 404: kategoria przychodzi w CIELE żądania — ta sama zasada co przy rozliczeniu rezerwacji.
        LimitCategoryInvalidException => (StatusCodes.Status400BadRequest, "Limit_CategoryInvalid"),
        // Żądanie poprawne, to historia limitów na nie nie pozwala — stąd 409.
        LimitHistoryLockedException => (StatusCodes.Status409Conflict, "Limit_HistoryLocked"),

        StandingOrderNameRequiredException => (StatusCodes.Status400BadRequest, "StandingOrder_NameRequired"),
        StandingOrderPatternInvalidException => (StatusCodes.Status400BadRequest, "StandingOrder_PatternInvalid"),
        StandingOrderRulesInvalidException => (StatusCodes.Status400BadRequest, "StandingOrder_RulesInvalid"),
        StandingOrderAmountInvalidException => (StatusCodes.Status400BadRequest, "StandingOrder_AmountInvalid"),
        StandingOrderDueMonthInvalidException => (StatusCodes.Status400BadRequest, "StandingOrder_DueMonthInvalid"),
        EpisodicOrderNameRequiredException => (StatusCodes.Status400BadRequest, "EpisodicOrder_NameRequired"),
        EpisodicOrderPlanInvalidException => (StatusCodes.Status400BadRequest, "EpisodicOrder_PlanInvalid"),
        EpisodicOrderTransactionInvalidException => (StatusCodes.Status400BadRequest, "EpisodicOrder_TransactionInvalid"),
        EpisodicOrderStateConflictException => (StatusCodes.Status409Conflict, "EpisodicOrder_StateConflict"),

        // Trzy razy ta sama historia: reguła zapisałaby się bez błędu i nigdy nie zadziałała.
        // 400, bo to wejście jest niepoprawne, a nie stan zasobu.
        RulePatternRequiredException =>
            (StatusCodes.Status400BadRequest, "Rule_PatternRequired"),
        RulePatternInvalidException =>
            (StatusCodes.Status400BadRequest, "Rule_PatternInvalid"),
        RuleAmountRangeInvalidException =>
            (StatusCodes.Status400BadRequest, "Rule_AmountRangeInvalid"),

        // Nigdy cichy fallback na „pierwszy z brzegu" — patrz EntityNotFoundException.
        EntityNotFoundException => (StatusCodes.Status404NotFound, null),

        UserNotAuthenticatedException => (StatusCodes.Status401Unauthorized, "Auth_NotAuthenticated"),
        OwnerReassignInvalidException => (StatusCodes.Status400BadRequest, "Admin_ReassignInvalid"),

        // ⚠️ To NIE jest wyjątek domenowy i nie ma go tu przez przypadek.
        // ASP.NET rzuca go przy zepsutym ciele żądania i sam niesie właściwy kod (400).
        // Bez tej gałęzi samo dołożenie UseExceptionHandler zamieniłoby niepoprawny JSON
        // w 500 — middleware przechwytuje wyjątek, zanim host zdąży odczytać jego status.
        //
        // ⚠️ Treść, nie `null`. Ten wyjątek leci TAKŻE przy niepoprawnym parametrze w adresie
        // (`?from=training` nie wiąże się do `DateOnly`), a wtedy odpowiedź bez ciała mówiła
        // frontowi dokładnie tyle, co awaria sieci — więc pokazywał „nie udało się połączyć
        // z serwerem", chociaż serwer odpowiedział i to on wiedział, co jest nie tak.
        // Inne kody z tego wyjątku (np. 413 przy za dużym żądaniu) zostają bez treści:
        // dobrane zdanie o filtrach byłoby tam nieprawdą.
        BadHttpRequestException bad => (
            bad.StatusCode,
            bad.StatusCode == StatusCodes.Status400BadRequest ? "Request_Invalid" : null),

        _ => null,
    };

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        if (Map(exception) is not { } mapped) return false;

        var (status, resourceKey) = mapped;

        // Poziom Information, nie Error: to normalna odpowiedź na złe wejście, a nie awaria.
        // Wpuszczenie tego do logów błędów zaszumiłoby je tak, że przestałyby cokolwiek znaczyć.
        //
        // Treść wyjątku JEST logowana, w odróżnieniu od odpowiedzi: przy błędzie wiązania niesie
        // nazwę parametru i wartość, która się nie związała („Failed to bind parameter …"), czyli
        // dokładnie to, czego szuka się przy diagnozie. W odpowiedzi tego nie ma, bo to szczegół
        // implementacji — użytkownik dostaje zdanie z `.resx` (CLAUDE.md §4).
        logger.LogInformation("{Exception} → {Status}: {Message}",
            exception.GetType().Name, status, exception.Message);

        context.Response.StatusCode = status;

        if (resourceKey is not null)
        {
            // Localizer z zakresu ŻĄDANIA, nie z konstruktora: handler jest singletonem,
            // a kultura ustala się per żądanie (UseRequestLocalization).
            var localizer = context.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();

            // Ten sam kształt ciała co przy walidacji — front czyta `error` niezależnie od kodu.
            //
            // `detail` WYŁĄCZNIE poza produkcją. Jest bezcenne przy diagnozie (mówi, który
            // parametr się nie związał), ale to komunikat frameworka: nietłumaczony i odsłaniający
            // sygnatury metod, więc nie ma czego szukać w odpowiedzi dla obcego.
            var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();

            await context.Response.WriteAsJsonAsync(
                new
                {
                    error = localizer[resourceKey].Value,
                    detail = environment.IsDevelopment() ? exception.Message : null,
                },
                ct);
        }

        return true;
    }
}
