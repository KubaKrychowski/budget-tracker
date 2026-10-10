# Jeden plan na obie aplikacje .NET — patrz zmienna `app_service_sku`.
#
# REWIZJA (2026-09-26): z F1 na B1. F1 przestał wystarczać nie z powodu wydajności, tylko dlatego, że trzy
# jego ograniczenia okazały się blokadami funkcjonalnymi:
#
#   * brak własnych domen i certyfikatu — a dopóki Identity stoi pod `*.azurewebsites.net`, a front pod
#     `*.azurestaticapps.net`, ciasteczko sesji jest ciasteczkiem TRZECIEJ STRONY. Ciche odnawianie sesji
#     działa wtedy najwyżej w Chrome i tylko do czasu;
#   * brak Always On — zadania Hangfire (trening modelu, `BudgetPurger`) NIE WYKONYWAŁY SIĘ, dopóki ktoś
#     nie wszedł na stronę i nie obudził procesu;
#   * 60 minut CPU na dobę, liczone per region per subskrypcja. Pętla awaryjna po jednej literówce
#     w connection stringu potrafiła to wyczerpać i wyłączyć obie aplikacje do północy UTC.
#
# ⚠️ B1 nalicza się od REZERWACJI, nie od zużycia: plan kosztuje za każdą godzinę istnienia, także przy
# zerowym ruchu i przy zatrzymanych aplikacjach. Zatrzymanie aplikacji NIE wstrzymuje naliczania —
# trzeba zejść z powrotem na F1 albo usunąć plan.

resource "azurerm_service_plan" "main" {
  name                = "${local.prefix}-plan"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = local.location

  os_type  = "Linux"
  sku_name = var.app_service_sku

  tags = local.tags
}

# ⚠️ Separator zagnieżdżenia to PODWÓJNY PODKREŚLNIK, nie dwukropek. App Service podaje ustawienia
# aplikacji jako zmienne środowiskowe, a dwukropek nie jest dozwolonym znakiem w nazwie zmiennej na
# Linuksie — `Identity:Issuer` po prostu nie dotrze do konfiguracji i objawi się jako „brak konfiguracji
# Identity:Issuer" przy starcie, mimo że w portalu ustawienie widać.

resource "azurerm_linux_web_app" "api" {
  name                = "${local.prefix}-api"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = azurerm_service_plan.main.location
  service_plan_id     = azurerm_service_plan.main.id

  https_only = true

  # Tożsamość zarządzana zamiast klucza konta magazynu — API używa DefaultAzureCredential i poza
  # Development NIE wyklucza ManagedIdentityCredential (patrz Program.cs).
  identity {
    type = "SystemAssigned"
  }

  site_config {
    # ⚠️ Oba ustawienia są WYMUSZONE przez F1, nie są wyborem: Always On jest w tym planie niedostępne,
    # a proces 64-bitowy też. Pozostawienie domyślnych wartości kończy się błędem przy `apply`.
    always_on         = local.supports_always_on
    use_32_bit_worker = !local.supports_always_on

    ftps_state          = "Disabled"
    minimum_tls_version = "1.2"

    # App Service odpytuje ten adres i nie kieruje ruchu do instancji, która nie odpowiada. Adres jest anonimowy
    # i nie dotyka danych (patrz Program.cs). ⚠️ NIE zmieniaj na /health/db: chwilowa niedostępność bazy
    # wyrzuciłaby wtedy z rotacji instancję, która sama jest zdrowa.
    health_check_path                 = "/health"
    health_check_eviction_time_in_min = 10

    application_stack {
      dotnet_version = "10.0"
    }
  }

  # Connection string telemetrii czytany wprost z zasobu Application Insights (monitoring.tf) — nie przechodzi
  # przez tfvars ani przez ręce, tak samo jak connection string poczty w Identity.
  app_settings = merge(
    {
      ASPNETCORE_ENVIRONMENT = "Production"

      # ⚠️ App Service konczy TLS na froncie i przekazuje zadanie do kontenera zwyklym HTTP, wiec
      # aplikacja widzi Request.IsHttps == false. Bez tego ustawienia antiforgery z SecurePolicy.Always
      # rzuca przy kazdym formularzu, a UseHttpsRedirection z UseHsts robi nieskonczone przekierowanie.
      # Wlacza middleware naglowkow przekazanych - bez zmiany w kodzie, patrz dokumentacja ASP.NET Core
      # "Configure ASP.NET Core to work with proxy servers and load balancers".
      ASPNETCORE_FORWARDEDHEADERS_ENABLED = "true"

      ConnectionStrings__Postgres = var.postgres_connection_string_api

      # Rola zadań systemowych (patrz variables.tf). Pusty string znaczy „brak” i włącza stary tor (patrz SystemDb).
      ConnectionStrings__PostgresWorker = var.postgres_connection_string_worker

      # Resource server: API waliduje tokeny przez JWKS serwera tożsamości, bez wspólnej bazy.
      Identity__Issuer = "${local.identity_url}/"

      Storage__BlobServiceUri      = azurerm_storage_account.models.primary_blob_endpoint
      Storage__SharedContainerName = azurerm_storage_container.shared.name
      Storage__TrainingSetName     = "training-set.csv"

      # OCR paragonow (receipts.tf). Pusty adres wylaczylby odczyt (API zwraca wtedy 503), reszta aplikacji dziala.
      Receipts__Endpoint = azurerm_cognitive_account.receipts.endpoint

      # Front woła API z innego originu (Static Web Apps), więc bez tej listy przeglądarka odrzuci każde
      # żądanie. Landing tu NIE jest wymieniony — to strona statyczna, która nie rozmawia z API.
      Cors__Origins__0 = local.front_url

      # WebView aplikacji mobilnej (Capacitor): Android serwuje ją spod https://localhost, iOS spod capacitor://localhost.
      # Muszą się zgadzać z OAuthDefaults.MobileWebViewOrigins w BudgetTracker.Identity.
      Cors__Origins__1 = "https://localhost"
      Cors__Origins__2 = "capacitor://localhost"
    },
    {
      APPLICATIONINSIGHTS_CONNECTION_STRING = azurerm_application_insights.main.connection_string
    }
  )

  tags = local.tags
}

resource "azurerm_linux_web_app" "identity" {
  name                = "${local.prefix}-identity"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = azurerm_service_plan.main.location
  service_plan_id     = azurerm_service_plan.main.id

  https_only = true

  site_config {
    always_on         = local.supports_always_on
    use_32_bit_worker = !local.supports_always_on

    ftps_state          = "Disabled"
    minimum_tls_version = "1.2"

    application_stack {
      dotnet_version = "10.0"
    }
  }

  # Listy (adresy powrotne SPA, adresy administratorów) App Service przyjmuje jako klucze z indeksem,
  # dlatego są doklejane przez `merge` zamiast wpisywane po jednym.
  app_settings = merge(
    {
      ASPNETCORE_ENVIRONMENT = "Production"

      # ⚠️ App Service konczy TLS na froncie i przekazuje zadanie do kontenera zwyklym HTTP, wiec
      # aplikacja widzi Request.IsHttps == false. Bez tego ustawienia antiforgery z SecurePolicy.Always
      # rzuca przy kazdym formularzu, a UseHttpsRedirection z UseHsts robi nieskonczone przekierowanie.
      # Wlacza middleware naglowkow przekazanych - bez zmiany w kodzie, patrz dokumentacja ASP.NET Core
      # "Configure ASP.NET Core to work with proxy servers and load balancers".
      ASPNETCORE_FORWARDEDHEADERS_ENABLED = "true"

      ConnectionStrings__Default = var.postgres_connection_string_identity

      # ⚠️ Issuer musi być DOKŁADNIE tym adresem, pod którym serwer odpowiada, razem z ukośnikiem na
      # końcu. Rozjazd tutaj nie psuje startu — psuje walidację tokenów po stronie API, czyli objawia
      # się jako „zalogowany użytkownik dostaje 401" i szuka się tego długo.
      Identity__Issuer = "${local.identity_url}/"
      Api__BaseUrl     = local.api_url

      # Bez trwałych kluczy każdy restart procesu wylogowuje wszystkich. Poza Development serwer NIE
      # WSTANIE bez tej wartości — to celowy fail-fast, żeby nie zgadywać katalogu (patrz Program.cs).
      DataProtection__KeysPath = local.data_protection_keys_path

      # Certyfikaty tokenów jako para PEM-ów w base64 — powstają w certificates.tf. Plik PFX w paczce
      # wdrożeniowej byłby materiałem kryptograficznym leżącym obok kodu i kasowanym przy każdym wydaniu.
      Identity__Certificates__Signing__Pem       = base64encode(tls_self_signed_cert.signing.cert_pem)
      Identity__Certificates__Signing__PemKey    = base64encode(tls_private_key.signing.private_key_pem_pkcs8)
      Identity__Certificates__Encryption__Pem    = base64encode(tls_self_signed_cert.encryption.cert_pem)
      Identity__Certificates__Encryption__PemKey = base64encode(tls_private_key.encryption.private_key_pem_pkcs8)

      Clients__Admin__Secret = random_password.admin_client.result

      Registration__ClosedBeta = tostring(var.registration_closed_beta)

      # Connection string czytany wprost z zasobu ACS - nie przechodzi przez tfvars ani przez rece.
      Acs__Email__ConnectionString = data.azurerm_communication_service.email.primary_connection_string
      Acs__Email__SenderAddress    = var.email_sender_address

      Clients__Landing__Origins__0            = local.landing_url
      Clients__Spa__RedirectUris__0           = "${local.front_url}/auth-callback"
      Clients__Spa__RedirectUris__1           = "${local.front_url}/silent-renew.html"
      Clients__Spa__PostLogoutRedirectUris__0 = "${local.front_url}/"
    },
    { for index, email in var.admin_emails : "Admin__Emails__${index}" => email }
  )

  tags = local.tags
}
