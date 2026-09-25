# Infrastruktura — Azure

Terraform stawia **miejsca**, w których ma zamieszkać aplikacja. Nie wgrywa kodu, nie zakłada bazy
i nie tworzy certyfikatów — te trzy rzeczy są opisane niżej jako kroki ręczne.

```
infra/
├── bootstrap/   # jednorazowo: konto magazynu na stan Terraforma (własny stan trzyma lokalnie)
└── *.tf         # właściwa infrastruktura; stan w Azure
```

## Co powstaje

| Zasób | Po co | Koszt |
|---|---|---|
| App Service Plan **F1** (Linux) | jeden plan na obie aplikacje .NET | 0 zł |
| Web App `-api` | API budżetu, z tożsamością zarządzaną | 0 zł |
| Web App `-identity` | serwer tożsamości | 0 zł |
| Static Web App `-front` | aplikacja (Angular) | 0 zł (plan Free) |
| Static Web App `-landing` | strona projektu | 0 zł (plan Free) |
| Storage Account `…models…` | modele kategoryzacji i zbiór uczący (lokalnie robi to Azurite) | grosze |
| Storage Account `…tfstate…` | stan Terraforma (moduł `bootstrap`) | grosze |

Baza **nie** powstaje: jest na Neonie, poza Terraformem, który dostaje gotowe connection stringi.

## Zanim uruchomisz — trzy rzeczy, które trzeba wiedzieć

### 1. F1 to plan z twardym sufitem, nie „mniejszy plan"

- **60 minut CPU na dobę**, liczone **per region per subskrypcja** i dzielone przez wszystkie darmowe
  aplikacje w tym regionie. Po przekroczeniu App Service odpowiada **403 do końca doby**. Import CSV
  i trening modelu potrafią to zjeść w kilka minut.
- **1 GB pamięci na cały plan** — dwa procesy ASP.NET dzielą się jednym gigabajtem.
- **165 MB transferu na dobę.**
- **Brak własnej domeny i własnego certyfikatu.** Adresy to `*.azurewebsites.net`. Dla Static Web Apps
  własne domeny są dostępne (2 na aplikację, certyfikat w cenie) — ograniczenie dotyczy App Service.

### 2. Brak Always On oznacza, że Hangfire nie działa

W F1 nie ma Always On. Proces jest usypiany po ok. 20 minutach bez ruchu, więc **zakolejkowane zadania
nie wykonają się**, dopóki ktoś nie wejdzie na stronę i nie obudzi procesu. Dotyczy to treningu modelu
i `BudgetPurger` (czyszczenie budżetów po okresie retencji). To nie jest „wolniej" — to „nie wykona się".

Pierwsze żądanie po uśpieniu to kilkanaście sekund oczekiwania.

### 3. Baza na Neonie: jedno pytanie wymaga sprawdzenia PRZED wdrożeniem

Schemat API wymaga roli `budget_jobs` z atrybutem `BYPASSRLS` (`api/db/setup-rls-roles.sql`). W Postgresie
atrybut `BYPASSRLS` może nadać **wyłącznie rola, która sama go ma**, a atrybutów ról nie dziedziczy się
przez członkostwo. Rola właściciela u Neona jest członkiem `neon_superuser` (który `BYPASSRLS` ma), ale to
nie to samo, co posiadanie atrybutu.

**Nie zgaduj — sprawdź jedną komendą na świeżym projekcie Neona:**

```bash
psql "<connection-string-neon>" -c "CREATE ROLE budget_jobs WITH NOLOGIN BYPASSRLS;"
```

Jeśli odpowie `permission denied`, Neon nie uniesie tego schematu bez zmiany podejścia do RLS i trzeba
wrócić do wyboru bazy. Cała reszta Terraforma jest od tego niezależna — dotyczy tylko wartości dwóch
zmiennych z connection stringami.

## Kolejność uruchomienia

### Krok 1 — bootstrap (raz na życie projektu)

```bash
cd infra/bootstrap && terraform init
```

```bash
terraform apply -var="subscription_id=<id>" -var="state_writer_principal_id=$(az ad signed-in-user show --query id -o tsv)"
```

### Krok 2 — backend.hcl (poza gitem)

Nazwa konta magazynu ma losowy przyrostek, więc nie da się jej wpisać na sztywno w repozytorium.

```bash
cd infra/bootstrap && terraform output -json backend_config | tee ../backend.hcl.json
```

Przepisz wartości do `infra/backend.hcl`:

```hcl
resource_group_name  = "wydatki-tfstate-rg"
storage_account_name = "wydatkitfstateab12cd"
container_name       = "tfstate"
key                  = "beta.tfstate"
use_azuread_auth     = true
```

### Krok 3 — właściwa infrastruktura

```bash
cd infra && cp terraform.tfvars.example terraform.tfvars
```

Uzupełnij `terraform.tfvars`, potem:

```bash
cd infra && terraform init -backend-config=backend.hcl && terraform plan
```

## Czego Terraform NIE robi

### Certyfikaty OpenIddict

Poza `Development` serwer tożsamości **nie wstanie** bez dwóch plików PFX: podpisującego tokeny
i szyfrującego kody autoryzacyjne. Terraform zna tylko ich ścieżki i hasła — same pliki muszą wjechać
w paczce wdrożeniowej do `/home/site/wwwroot/certs/`.

⚠️ Kto ma certyfikat podpisujący, może wystawić token dowolnego użytkownika. Nie trzymaj go w repozytorium,
nawet prywatnym, i nie używaj tego samego, co lokalnie.

```bash
openssl req -x509 -newkey rsa:2048 -keyout signing.key -out signing.crt -days 730 -nodes -subj "/CN=wydatki-identity-signing"
```

```bash
openssl pkcs12 -export -out signing.pfx -inkey signing.key -in signing.crt
```

To samo dla `encryption.pfx`. Hasła trafiają do `terraform.tfvars`.

**Wygodniejsza alternatywa na później:** zmienić kod tak, żeby czytał certyfikat z zmiennej środowiskowej
(base64) zamiast z pliku. Wtedy Terraform ogarnia całość i nie ma kroku ręcznego. Dziś `ServerCertificateLoader`
czyta ścieżkę, więc zostaje paczka.

### Poczta

`SmtpOptions` ma `ValidateOnStart`, więc bez poprawnej konfiguracji serwer tożsamości **nie wstanie** —
a rejestracja i tak wymaga maila z potwierdzeniem adresu. Azure nie ma darmowego SMTP, a App Service
blokuje port 25; potrzebny dostawca z zewnątrz na porcie 587.

### Wdrożenie kodu

Terraform tworzy puste miejsca. Kod wgrywa się osobno:

- **Static Web Apps** — akcja GitHuba `Azure/static-web-apps-deploy`, token z wyjścia:

```bash
cd infra && terraform output -raw front_deployment_token
```

- **App Service** — `az webapp deploy` albo akcja `azure/webapps-deploy` z paczką z `dotnet publish`.

### Migracje bazy

`dotnet ef database update` osobno, na obu bazach. API **nie migruje się samo** przy starcie.

## Rzeczy, które łatwo przeoczyć

- **Region Static Web Apps to osobna zmienna.** Usługa istnieje tylko w `westeurope`, `eastus2`,
  `centralus`, `westus2`, `eastasia`. Wpisanie tam regionu App Service kończy się błędem przy `apply`.
- **Separator ustawień to `__`, nie `:`.** Na Linuksie dwukropek jest niedozwolony w nazwie zmiennej
  środowiskowej — ustawienie widać w portalu, a aplikacja go nie widzi.
- **Connection string API musi używać roli `budget_app`, nie właściciela bazy.** RLS nie dotyczy
  właściciela tabel, więc połączenie właścicielem po cichu wyłącza izolację danych między kontami.
- **Plan Free Static Web Apps nie ma „linked backend".** Front woła API między originami, przez CORS —
  dlatego adres frontu trafia do ustawień API, a `UseCors` w API obowiązuje w każdym środowisku.
- **W stanie Terraforma leżą sekrety jawnym tekstem.** Dlatego konto magazynu na stan ma wyłączone klucze
  dostępu i wymusza tożsamość Entra ID, a `terraform.tfvars` i `backend.hcl` są w `.gitignore`.
