namespace BudgetTracker.Api.Infrastructure;

/// <summary>
/// Owija uwierzytelnione żądanie API w transakcję bazodanową, żeby ODCZYTY też działały pod poolerem Neona.
/// </summary>
/// <remarks>
/// <para>
/// Bez transakcji EF wykonuje każde zapytanie osobno (autocommit). Za PgBouncerem w trybie transakcyjnym każde
/// takie zapytanie to osobna transakcja na potencjalnie INNYM backendzie, więc <c>SET LOCAL app.current_user_id</c>
/// z <see cref="RlsTransactionInterceptor"/> nie dojechałby do zapytania. Objęcie całego żądania jedną transakcją
/// sprawia, że <c>SET LOCAL</c> i wszystkie zapytania żądania są na jednym backendzie.
/// </para>
/// <para>
/// ⚠️ Filtr endpointu, nie middleware — filtr działa WOKÓŁ handlera, a <c>COMMIT</c> po <c>next()</c> wypada
/// PRZED serializacją <see cref="IResult"/>. Gdyby to było middleware, wynik zdążyłby się już zapisać do
/// odpowiedzi, zanim commit by się wykonał.
/// </para>
/// <para>
/// Pomijamy, gdy: brak zalogowanego użytkownika (endpoint anonimowy — nie dotyka danych właściciela) albo
/// transakcja już istnieje (nie zagnieżdżamy). Wyjątek w handlerze → <c>await using</c> wycofuje transakcję.
/// </para>
/// </remarks>
public sealed class RlsTransactionEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var currentUser = http.RequestServices.GetRequiredService<ICurrentUserAccessor>();
        var db = http.RequestServices.GetRequiredService<AppDbContext>();

        if (currentUser.UserIdOrNull is null || db.Database.CurrentTransaction is not null)
        {
            return await next(context);
        }

        var ct = http.RequestAborted;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await next(context);
        await transaction.CommitAsync(ct);
        return result;
    }
}
