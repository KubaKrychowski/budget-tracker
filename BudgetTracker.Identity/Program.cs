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

builder.Services.AddHostedService<OpenIddictSeeder>();
builder.Services.AddHostedService<OpenIddictPruningService>();

builder.Services.AddOptions<TokenLifetimeOptions>().Bind(builder.Configuration.GetSection(TokenLifetimeOptions.SectionName));
builder.Services.AddOptions<AdminOptions>().Bind(builder.Configuration.GetSection(AdminOptions.SectionName));
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
