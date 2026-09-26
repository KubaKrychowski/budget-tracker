using BudgetTracker.Identity.Data;
using BudgetTracker.Identity.Infrastructure;
using BudgetTracker.Identity.Models;
using BudgetTracker.Identity.Options;
using BudgetTracker.Identity.Resources;
using BudgetTracker.Identity.Services;
using BudgetTracker.Identity.Services.Api;
using BudgetTracker.Identity.Services.Audit;
using BudgetTracker.Identity.Services.Emails;
using BudgetTracker.Identity.Services.Users;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization();

// ErrorMessage w atrybutach walidacji modeli to klucz z SharedResource — patrz AccountViewModels.
builder.Services.AddControllersWithViews()
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)));

// Bez trwałych kluczy ochrony danych są efemeryczne — każdy restart procesu wylogowywałby wszystkich, bo
// ciasteczko przestaje się dać odszyfrować. Kto zdobędzie te klucze, może podrobić ciasteczko logowania, więc
// poza Development katalog jest WYMAGANY i leży poza repo (fail-fast, żeby nie zgadywać domyślnej lokalizacji).
var dataProtection = builder.Services.AddDataProtection().SetApplicationName(HostingDefaults.ApplicationName);
if (builder.Environment.IsDevelopment())
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Path.GetTempPath(), "bt-identity-keys")));
}
else
{
    var keysPath = builder.Configuration[HostingDefaults.DataProtectionKeysPathKey]
        ?? throw new InvalidOperationException($"Brak konfiguracji {HostingDefaults.DataProtectionKeysPathKey}.");
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

// Ciasteczka tylko po https (dev też działa na https — patrz launchSettings): SameAsRequest przepuściłoby
// ciasteczko logowania po zwykłym http, gdzie da się je podsłuchać.
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);

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
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    // ⚠️ SameSite zostaje DOMYŚLNY (Lax) i ma taki zostać.
    //
    // Przez chwilę stało tu `SameSiteMode.None`, bo front siedział na Static Web Apps, a Identity na
    // App Service — różne witryny, więc ciasteczko sesji było ciasteczkiem TRZECIEJ STRONY i przeglądarka
    // nie wysyłała go w ukrytej ramce cichego odnawiania. Objawem był 401 po wygaśnięciu tokenu.
    //
    // Od czasu przejścia na własną domenę (`app.*` i `auth.*` tej samej domeny rejestrowalnej) problem
    // zniknął u źródła: to jedna witryna, więc Lax wystarcza. `None` byłoby teraz nie tylko zbędne, ale
    // i słabsze — otwierałoby ciasteczko na wysyłkę z DOWOLNEJ obcej strony, a właśnie przed tym Lax
    // chroni. Safari, które ciasteczka trzeciej strony blokuje niezależnie od SameSite, też przez to działa.
    //
    // ⚠️ To NIE znosi poluzowania `frame-ancestors` w SecurityHeadersMiddleware — tamto dotyczy osadzania
    // ramki, nie ciasteczek, i dalej jest potrzebne, żeby ciche odnawianie w ogóle mogło się wykonać.
});

var identityIssuer = builder.Configuration[OAuthDefaults.IssuerConfigKey]
    ?? throw new InvalidOperationException($"Brak konfiguracji {OAuthDefaults.IssuerConfigKey}.");

var tokenLifetimes = builder.Configuration.GetSection(TokenLifetimeOptions.SectionName).Get<TokenLifetimeOptions>()
    ?? new TokenLifetimeOptions();

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
            Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess, OAuthDefaults.ApiScope,
            OAuthDefaults.AdminApiScope);

        options
            .AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()
            .AllowRefreshTokenFlow()
            .AllowPasswordFlow()
            .AllowClientCredentialsFlow();

        // Tokeny dostępu jako podpisane JWT (nie szyfrowane) — API weryfikuje je zdalnie przez
        // klucze publiczne z /.well-known/jwks (patrz JwtBearer w BudgetTracker.Api), bez wspólnej bazy.
        options.DisableAccessTokenEncryption();

        options
            .SetAccessTokenLifetime(tokenLifetimes.AccessToken)
            .SetIdentityTokenLifetime(tokenLifetimes.IdentityToken)
            .SetRefreshTokenLifetime(tokenLifetimes.RefreshToken);

        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();
        }
        else
        {
            // ⚠️ Certyfikat podpisujący pozwala wystawić dowolny token dla dowolnego użytkownika — poza
            // Development musi być prawdziwy, spoza repo, a serwer nie startuje bez niego.
            var certificates = builder.Configuration.GetSection(ServerCertificatesOptions.SectionName)
                .Get<ServerCertificatesOptions>();
            options.AddSigningCertificate(ServerCertificateLoader.Load(certificates?.Signing, "podpisujący"));
            options.AddEncryptionCertificate(ServerCertificateLoader.Load(certificates?.Encryption, "szyfrujący"));
        }

        // Wymóg https zostaje włączony wszędzie, także w Development: lokalnie też działamy na https.
        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableUserInfoEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough()
            .EnableStatusCodePagesIntegration();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddSingleton<FixedAccountSeeder>();
builder.Services.AddHostedService<OpenIddictSeeder>();
builder.Services.AddHostedService<OpenIddictPruningService>();

builder.Services.AddOptions<TokenLifetimeOptions>().Bind(builder.Configuration.GetSection(TokenLifetimeOptions.SectionName));
builder.Services.AddOptions<AdminOptions>().Bind(builder.Configuration.GetSection(AdminOptions.SectionName));
builder.Services.AddOptions<RegistrationOptions>().Bind(builder.Configuration.GetSection(RegistrationOptions.SectionName));
builder.Services.AddOptions<ApiOptions>()
    .Bind(builder.Configuration.GetSection(ApiOptions.SectionName))
    .Validate(options => options.HasValidBaseUrl, $"Brak albo zły adres https w konfiguracji {ApiOptions.SectionName}:BaseUrl.")
    .ValidateOnStart();

// Usuwanie kont: serwer tożsamości woła /api/admin API budżetu tokenem serwisowym (client credentials), który
// pobiera od WŁASNEGO endpointu /connect/token (adres = issuer).
builder.Services.AddHttpClient(ServiceTokenProvider.HttpClientName, client =>
    client.BaseAddress = new Uri(identityIssuer.TrimEnd('/') + "/"));
builder.Services.AddHttpClient(ApiUserDataClient.HttpClientName, (services, client) =>
    client.BaseAddress = new Uri(services.GetRequiredService<IOptions<ApiOptions>>().Value.BaseUrl.TrimEnd('/') + "/"));
builder.Services.AddSingleton<ServiceTokenProvider>();
builder.Services.AddScoped<IUserDataClient, ApiUserDataClient>();
builder.Services.AddScoped<IAccountStore, IdentityAccountStore>();
builder.Services.AddScoped<IAdminAuditLog, DbAdminAuditLog>();
builder.Services.AddScoped<IAdminAuditReader, DbAdminAuditReader>();
builder.Services.AddScoped<UserDeletionService>();
builder.Services.AddScoped<AdminRoleService>();
builder.Services.AddScoped<BetaInviteService>();

// Dwie drogi wysyłki, wybór należy do KONFIGURACJI, nie do kodu: wypełniona sekcja Acs:Email wygrywa,
// pusta zostawia SMTP (Development i MailHog). Na Azure idzie ACS, bo klucz dostępu z portalu działa
// wyłącznie przez SDK — przekaźnik SMTP wymagałby osobnego zasobu „SMTP Username" i rejestracji aplikacji
// w Entra ID, czyli trzech bytów do założenia ręcznie.
builder.Services.AddOptions<AcsEmailOptions>().Bind(builder.Configuration.GetSection(AcsEmailOptions.SectionName));
builder.Services.AddOptions<SmtpOptions>().Bind(builder.Configuration.GetSection(SmtpOptions.SectionName));

var acsEmail = builder.Configuration.GetSection(AcsEmailOptions.SectionName).Get<AcsEmailOptions>();

if (acsEmail?.IsConfigured == true)
{
    builder.Services.AddSingleton<IEmailSender, AcsEmailSender>();
}
else
{
    // ⚠️ Reguła walidacji, a nie samo ValidateOnStart(). Do 2026-09-25 stało tu gołe ValidateOnStart(),
    // które bez żadnej reguły NICZEGO nie sprawdza — wiązanie konfiguracji nie wymusza `required`, więc
    // brak sekcji dawał Host = null, serwer wstawał normalnie, a wywalała się dopiero pierwsza rejestracja.
    // Błąd konfiguracji ma wyjść przy starcie, a nie u pierwszego użytkownika, który zakłada konto.
    builder.Services.AddOptions<SmtpOptions>()
        .Validate(
            options => options.IsUsable,
            $"Brak konfiguracji poczty: wypełnij {AcsEmailOptions.SectionName} (ConnectionString + SenderAddress) albo {SmtpOptions.SectionName} (Host + FromAddress).")
        .ValidateOnStart();

    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
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

app.UseMiddleware<SecurityHeadersMiddleware>();

// W Development nie ma czego przekierowywać: serwer słucha wyłącznie na https (launchSettings). Przekierowanie
// 307 z http nie niesie nagłówków CORS, więc gdyby ktoś wystawił też http, fetch() z SPA kończyłby się
// błędem CORS zanim dotarłby do adresu docelowego.
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
