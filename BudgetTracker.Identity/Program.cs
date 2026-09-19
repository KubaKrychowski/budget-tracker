using BudgetTracker.Identity.Data;
using BudgetTracker.Identity.Infrastructure;
using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Options;
using BudgetTracker.Identity.Resources;
using BudgetTracker.Identity.Services;
using BudgetTracker.Identity.Services.Emails;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization();

// ErrorMessage w atrybutach walidacji modeli to klucz z SharedResource — patrz AccountViewModels.
builder.Services.AddControllersWithViews()
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

if (builder.Environment.IsDevelopment())
{
    // Bez tego klucze ochrony danych są efemeryczne — każdy restart procesu (np. po zmianie
    // kodu) wylogowywałby wszystkich, bo ciasteczko przestaje się dać odszyfrować.
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "bt-identity-keys")));
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
    options.UseOpenIddict();
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        // Rejestracja wysyła link potwierdzający (patrz AccountController.Register) — bez
        // kliknięcia w niego logowanie ma być zablokowane, inaczej mail byłby czysto dekoracyjny.
        options.SignIn.RequireConfirmedAccount = true;
        options.Password.RequiredLength = PasswordPolicy.MinLength;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;

        // Anty-bruteforce: blokada konta po 3 nieudanych próbach logowania (patrz Login w
        // AccountController — PasswordSignInAsync wywoływane z lockoutOnFailure: true).
        // Odzyskanie dostępu przed upływem czasu blokady: ResetPassword po kliknięciu w link
        // z ForgotPassword celowo czyści też blokadę (patrz AccountController.ResetPassword).
        options.Lockout.MaxFailedAccessAttempts = 3;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddErrorDescriber<LocalizedIdentityErrorDescriber>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var identityIssuer = builder.Configuration[OAuthDefaults.IssuerConfigKey]
    ?? throw new InvalidOperationException($"Brak konfiguracji {OAuthDefaults.IssuerConfigKey}.");

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore().UseDbContext<ApplicationDbContext>();
    })
    .AddServer(options =>
    {
        options
            .SetIssuer(new Uri(identityIssuer))
            .SetAuthorizationEndpointUris("connect/authorize")
            .SetTokenEndpointUris("connect/token")
            .SetUserInfoEndpointUris("connect/userinfo")
            .SetEndSessionEndpointUris("connect/logout");

        options.RegisterScopes(
            Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess, OAuthDefaults.ApiScope);

        options
            .AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()
            .AllowRefreshTokenFlow()
            .AllowPasswordFlow();

        // Tokeny dostępu jako podpisane JWT (nie szyfrowane) — API weryfikuje je zdalnie przez
        // klucze publiczne z /.well-known/jwks (patrz JwtBearer w BudgetTracker.Api), bez wspólnej bazy.
        options.DisableAccessTokenEncryption();

        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();
        }

        var aspNetCoreBuilder = options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableUserInfoEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough()
            .EnableStatusCodePagesIntegration();

        // Dev/demo działają po zwykłym http (patrz CLAUDE.md — Rider/ng serve na plain http) —
        // w produkcji to zdejmiemy, bo tam wymóg https jest pożądany.
        if (builder.Environment.IsDevelopment())
        {
            aspNetCoreBuilder.DisableTransportSecurityRequirement();
        }
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddHostedService<OpenIddictSeeder>();

builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<EmailTemplateRenderer>();
builder.Services.AddScoped<IAccountEmailService, AccountEmailService>();

builder.Services.AddOptions<SpaClientOptions>().Bind(builder.Configuration.GetSection(SpaClientOptions.SectionName));
builder.Services.AddSingleton<SpaOrigins>();

// SPA odpytuje discovery/JWKS i wymienia kod na token przez fetch() z INNEGO originu
// (localhost:4200/4310) — to podlega CORS, w przeciwieństwie do przekierowania na /connect/authorize
// (pełna nawigacja, CORS jej nie dotyczy). Te same originy co zarejestrowane redirect_uris klienta SPA.
const string spaCors = "spa-client";
builder.Services.AddOptions<CorsOptions>().Configure<SpaOrigins>((cors, spa) =>
    cors.AddPolicy(spaCors, policy => policy
        .WithOrigins([.. spa.All])
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error/Index");
    app.UseHsts();
}

// Domyślnie polski (jak API); język przeglądarki (Accept-Language) wybiera angielski, gdy go zażąda.
var supportedCultures = new[] { new CultureInfo("pl"), new CultureInfo("en") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("pl"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
});

// W Development NIE przekierowujemy na https: SPA na plain http (localhost:4200/4310) robi
// fetch() na discovery/token, a przekierowanie 307 nie niesie nagłówków CORS — przeglądarka
// blokuje to jako błąd CORS, zanim w ogóle dotrze do docelowego adresu (ten sam powód, dla
// którego OpenIddict ma wyżej DisableTransportSecurityRequirement w Development).
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseCors(spaCors);

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();
