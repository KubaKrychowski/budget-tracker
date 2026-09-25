# Jeden plan F1 na obie aplikacje .NET. Limit to 10 aplikacji na plan, więc dwie mieszczą się bez problemu
# — problemem jest to, CO dzielą:
#
#   * 60 minut CPU na dobę (per region per subskrypcja, nie per aplikacja). Po przekroczeniu App Service
#     odpowiada 403 do końca doby. Import CSV i trening modelu potrafią zjeść to w kilka minut.
#   * 1 GB pamięci NA CAŁY PLAN — dwa procesy ASP.NET dzielą się jednym gigabajtem.
#   * 165 MB transferu na dobę.
#
# ⚠️ Always On nie istnieje w F1. Konsekwencja nie jest kosmetyczna: proces jest usypiany po ok. 20
# minutach bez ruchu, więc zadania Hangfire (trening modelu, BudgetPurger) NIE WYKONAJĄ SIĘ, dopóki ktoś
# nie wejdzie na stronę i nie obudzi procesu. Pierwsze żądanie po uśpieniu to kilkanaście sekund.

resource "azurerm_service_plan" "main" {
  name                = "${local.prefix}-plan"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = local.location

  os_type  = "Linux"
  sku_name = "F1"

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
    always_on         = false
    use_32_bit_worker = true

    ftps_state          = "Disabled"
    minimum_tls_version = "1.2"

    application_stack {
      dotnet_version = "10.0"
    }
  }

  app_settings = {
    ASPNETCORE_ENVIRONMENT = "Production"

    ConnectionStrings__Postgres = var.postgres_connection_string_api

    # Resource server: API waliduje tokeny przez JWKS serwera tożsamości, bez wspólnej bazy.
    Identity__Issuer = "${local.identity_url}/"

    Storage__BlobServiceUri      = azurerm_storage_account.models.primary_blob_endpoint
    Storage__SharedContainerName = azurerm_storage_container.shared.name
    Storage__TrainingSetName     = "training-set.csv"

    # Front woła API z innego originu (Static Web Apps), więc bez tej listy przeglądarka odrzuci każde
    # żądanie. Landing tu NIE jest wymieniony — to strona statyczna, która nie rozmawia z API.
    Cors__Origins__0 = local.front_url
  }

  tags = local.tags
}

resource "azurerm_linux_web_app" "identity" {
  name                = "${local.prefix}-identity"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = azurerm_service_plan.main.location
  service_plan_id     = azurerm_service_plan.main.id

  https_only = true

  site_config {
    always_on         = false
    use_32_bit_worker = true

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

      # ⚠️ Wymagane, choć CLI nie jest tu wdrazane: OpenIddictSeeder zaklada tego klienta bezwarunkowo
      # i bez sekretu rzuca wyjatkiem, wiec serwer tozsamosci nie wstanie na swiezej bazie.
      Clients__Cli__Secret = random_password.cli_client.result

      Registration__ClosedBeta = tostring(var.registration_closed_beta)

      # Connection string czytany wprost z zasobu ACS - nie przechodzi przez tfvars ani przez rece.
      Acs__Email__ConnectionString = data.azurerm_communication_service.email.primary_connection_string
      Acs__Email__SenderAddress    = var.email_sender_address

      Clients__Spa__RedirectUris__0           = "${local.front_url}/auth-callback"
      Clients__Spa__RedirectUris__1           = "${local.front_url}/silent-renew.html"
      Clients__Spa__PostLogoutRedirectUris__0 = "${local.front_url}/"
    },
    { for index, email in var.admin_emails : "Admin__Emails__${index}" => email }
  )

  tags = local.tags
}
