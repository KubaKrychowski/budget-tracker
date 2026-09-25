# Infrastruktura — Azure

Terraform stawia **miejsca**, w których ma zamieszkać aplikacja, i generuje certyfikaty tokenów.
Nie wgrywa kodu, nie zakłada bazy i nie konfiguruje poczty — te trzy rzeczy są opisane niżej jako kroki ręczne.

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

## Zanim uruchomisz — cztery rzeczy, które trzeba wiedzieć

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

### 3. Baza na Neonie: rola właściciela omija RLS

Schemat API wymaga roli `budget_jobs` z atrybutem `BYPASSRLS` (`api/db/setup-rls-roles.sql`), a ten atrybut
może nadać wyłącznie rola, która sama go ma. **Sprawdzone 2026-09-25 na prawdziwym projekcie: Neon to
unosi.** `neondb_owner` ma `rolbypassrls` i `rolcreaterole` jako własne atrybuty, więc `setup-rls-roles.sql`
idzie bez zmian.

⚠️ **Ta sama właściwość jest najgroźniejszą pułapką tego wdrożenia.** Skoro `neondb_owner` omija RLS,
connection string wskazujący tę rolę **wyłącza izolację danych między kontami po cichu** — nic się nie psuje,
nic nie krzyczy, a każdy zalogowany widzi cudze budżety. Dlatego `postgres_connection_string_api` musi
wskazywać `budget_app`, nigdy właściciela bazy.

Sprawdzenie po migracji, połączony jako `budget_app`:

```bash
psql "<connection-string-budget-app>" -c "SELECT count(*) FROM \"Budgets\";"
```

Musi zwrócić **0** — nikt nie ustawił `app.current_user_id`, więc polityka nie przepuszcza żadnego wiersza.
Prawdziwa liczba w odpowiedzi znaczy, że łączysz się złą rolą i izolacja nie działa.

Stan docelowy ról w klastrze (`SELECT rolname, rolbypassrls, rolcanlogin FROM pg_roles`):

| rola | `rolbypassrls` | `rolcanlogin` | po co |
|---|---|---|---|
| `budget_app` | `f` | `t` | appka na co dzień — RLS JEJ DOTYCZY |
| `budget_jobs` | `t` | `f` | zadania bez kontekstu użytkownika, wchodzi się w nią `SET LOCAL ROLE` |

Do tego `GRANT budget_jobs TO budget_app`, inaczej `SET LOCAL ROLE` kończy się błędem.

### 4. Certyfikaty tokenów generuje Terraform i wymiana ich wylogowuje wszystkich

⚠️ Nie mylić z HTTPS. Certyfikat TLS dla `*.azurewebsites.net` **Azure daje sam** i nic się o niego nie robi.
Te dwa to materiał kryptograficzny aplikacji: podpisujący kładzie podpis pod każdym wydanym JWT (API
sprawdza go przez JWKS), szyfrujący chroni kody autoryzacyjne i refresh tokeny. Azure nie wie, że OpenIddict
istnieje, więc nie ma czego wygenerować — robi to `certificates.tf`, a klucze jadą do aplikacji jako para
PEM-ów w base64.

Samopodpisany jest tu **poprawny, nie jest kompromisem**: nikt nie weryfikuje łańcucha zaufania tego
certyfikatu, bo API bierze klucz publiczny z JWKS serwera tożsamości, a nie z urzędu certyfikacji.
Certyfikat jest tylko opakowaniem na parę kluczy.

Dwie konsekwencje, o których trzeba wiedzieć **zanim**, a nie po fakcie:

- **klucz podpisujący leży w stanie Terraforma.** Kto go ma, wystawi token dowolnego użytkownika — to
  groźniejszy sekret niż hasło do bazy, które leży tam obok. Dlatego konto magazynu na stan ma wyłączone
  klucze dostępu i wymusza tożsamość Entra ID;
- **wymiana certyfikatu unieważnia wszystkie wydane tokeny i wylogowuje wszystkich.** Ważność jest długa
  (domyślnie 5 lat), a `early_renewal_hours = 0`, więc Terraform nie wymieni ich sam przy okazji
  niepowiązanego `apply`. Po wygaśnięciu serwer tożsamości **nie wstanie** — loader odrzuca przeterminowany
  certyfikat, zamiast wystawiać tokeny podpisane byle czym. Wymiana jest więc zadaniem do zaplanowania.

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

### Poczta — SMTP Username w Azure Communication Services

`SmtpOptions` ma `ValidateOnStart`, więc bez poprawnej konfiguracji serwer tożsamości **nie wstanie** —
a rejestracja i tak wymaga maila z potwierdzeniem adresu.

Pocztę daje **Azure Communication Services**, przez przekaźnik SMTP: `smtp.azurecomm.net`, port 587,
STARTTLS. Kod nie wymaga żadnej zmiany — MailKit rozmawia z tym jak z każdym innym serwerem.

⚠️ Terraform **nie zarządza** tymi zasobami: ACS, Email Service i domena stoją we własnej grupie zasobów,
osobno od tego, co stawia `infra/`. Tutaj podaje się wyłącznie gotowe dane logowania.

Uwierzytelnianie nie jest zwykłą parą login–hasło. Trzeba trzech rzeczy:

1. **Rejestracja aplikacji w Entra ID** z sekretem klienta — sekret będzie **hasłem** SMTP.
2. **Rola na zasobie ACS** dla tej aplikacji: wbudowana `Communication and Email Service Owner` albo rola
   własna z `Microsoft.Communication/CommunicationServices/read` i `/write` oraz
   `Microsoft.Communication/EmailServices/write`.
3. **Zasób „SMTP Username"** w ACS, powiązany z tą aplikacją (portal → zasób ACS → *SMTP Usernames*).
   Nazwa jest dowolna; jeśli użyjesz formatu adresu e-mail, domena musi być jedną z podpiętych.
   To ona idzie do `smtp.username`, a nie identyfikator aplikacji.

Adres nadawcy przy domenie zarządzanej przez Azure ma postać `DoNotReply@<guid>.azurecomm.net` — odczytasz
go w zasobie Email Service jako `mailFromSenderDomain`.

⚠️ Port 25 odpada: App Service go blokuje, a i tak zalecany jest 587.

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
