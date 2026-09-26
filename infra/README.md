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

**Grupa zasobów musi już istnieć** — podajesz ją w `resource_group_name`, a Terraform jej nie tworzy
i nie usunie. Stoi w niej także to, czego Terraform nie zna (ACS z domeną poczty), więc `destroy` nie ma
prawa jej ruszyć. Region wszystkich zasobów bierze się z tej grupy; Static Web Apps ma własną zmienną,
bo istnieje tylko w pięciu regionach.

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

## Własna domena

Ustawia się ją zmienną `custom_domain` (np. `"wydatki.com"`). Puste = zostajemy na adresach nadanych
przez Azure i nic się nie wiąże.

⚠️ **To nie jest kosmetyka.** Dopóki front i serwer tożsamości stoją pod różnymi domenami, ciasteczko
sesji Identity jest ciasteczkiem **trzeciej strony**: ciche odnawianie sesji działa najwyżej w Chrome,
a w Safari nie działa wcale. Wspólna domena rejestrowalna rozwiązuje to u źródła.

⚠️ **Wymaga planu B1 lub wyższego.** F1 i D1 nie obsługują własnych domen ani certyfikatów.

**Kolejność jest wymuszona i nie da się jej skrócić:**

1. `terraform apply` **bez** `custom_domain` — zasoby muszą istnieć, żeby było na co kierować DNS,
2. `terraform output dns_do_zalozenia` — wypisze rekordy do założenia,
3. rekordy w Cloudflare, **proxy wyłączone** (szara chmurka). Przy włączonym Azure widzi adresy
   Cloudflare'a zamiast swoich i weryfikacja własności nie przechodzi,
4. `terraform apply` **z** `custom_domain` — wiązania nazw, certyfikaty zarządzane i podpięcie ich
   do wiązań. Terraform wyprowadza tę kolejność z zależności, więc pilnuje jej sam.

Landing siedzi na samej domenie (bez członu), więc jego własność potwierdza się rekordem **TXT**, a nie
CNAME — na szczycie domeny nie wolno postawić CNAME-a obok innych rekordów. Cloudflare spłaszcza CNAME
na szczycie, więc sam ruch zadziała.

Po związaniu domen trzeba jeszcze przestawić zmienne repozytorium `IDENTITY_URL` i `API_URL`
w GitHubie — front czyta je do `config.json` przy budowaniu.

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

### Sekrety przez zmienne środowiskowe

Terraform czyta każdą zmienną z `TF_VAR_<nazwa>`, więc sekrety nie muszą przejść przez żaden plik.
Cztery wartości są tak pomyślane i **nie ma ich w `terraform.tfvars.example`**:

| zmienna Terraforma | zmienna środowiskowa |
|---|---|
| `postgres_connection_string_api` | `TF_VAR_postgres_connection_string_api` |
| `postgres_connection_string_identity` | `TF_VAR_postgres_connection_string_identity` |

Connection stringa do poczty **nie ma na tej liście** — Terraform czyta go wprost z zasobu ACS.

Sekretów klientów OAuth (`budgettracker-admin`, `bt-cli`) **nie podajesz** — generuje je Terraform
(`secrets.tf`). Są wewnętrzne: serwer tożsamości sam je zapisuje w swojej bazie i sam ich używa, więc
nie ma drugiej strony, która musiałaby je poznać.

Na stałe, dla swojego konta w Windows (nowa sesja terminala je zobaczy):

```powershell
[Environment]::SetEnvironmentVariable('TF_VAR_postgres_connection_string_api', '<wartosc>', 'User')
```

Tylko na czas jednej sesji:

```powershell
$env:TF_VAR_postgres_connection_string_api = '<wartosc>'
```

⚠️ **Co to daje, a czego nie daje.** Sekret nie trafia do repozytorium ani do `terraform.tfvars` — to jest
realna korzyść. Ale **trafia do stanu Terraforma i do ustawień App Service**, bo inaczej aplikacja nie
działa. Zmienna środowiskowa zmienia to, kto widzi wartość *po drodze*, a nie to, gdzie ona ostatecznie
leży. Stąd konto magazynu na stan z wyłączonymi kluczami dostępu.

Wszystkie te zmienne są oznaczone `sensitive`, więc `terraform plan` pokazuje `(sensitive value)`
zamiast treści.

### Krok 3 — właściwa infrastruktura

```bash
cd infra && cp terraform.tfvars.example terraform.tfvars
```

Uzupełnij `terraform.tfvars`, potem:

```bash
cd infra && terraform init -backend-config=backend.hcl && terraform plan
```

## Czego Terraform NIE robi

### Poczta — nic do zrobienia, o ile ACS już stoi

Maile (potwierdzenie adresu, reset hasła, kod 2FA) idą przez **Azure Communication Services**, jego
**SDK**, nie przez przekaźnik SMTP. Terraform czyta connection string wprost z zasobu ACS, więc nie
przechodzi on ani przez `terraform.tfvars`, ani przez zmienną środowiskową, ani przez niczyje ręce.
W `terraform.tfvars` podaje się tylko dwie jawne wartości: `communication_service_name`
i `email_sender_address`.

⚠️ Terraform tych zasobów **nie zarządza**. ACS, Email Service i domena mają przeżyć każdy `destroy` —
stąd wyłącznie odczyt.

⚠️ **Klucz dostępu ACS z portalu działa TYLKO tą drogą.** Przekaźnik `smtp.azurecomm.net` go nie przyjmuje:
tamta droga wymaga rejestracji aplikacji w Entra ID, roli `Communication and Email Service Owner` na
zasobie ACS oraz osobnego zasobu „SMTP Username" — trzech bytów do założenia ręcznie. Dlatego kod ma dwie
implementacje `IEmailSender`, a wybiera je konfiguracja: wypełniona sekcja `Acs:Email` wygrywa, pusta
zostawia SMTP (Development i MailHog).

⚠️ `email_sender_address` musi należeć do domeny podpiętej do tego ACS, inaczej wysyłka jest odrzucana.
Przy domenie zarządzanej przez Azure ma postać `DoNotReply@<guid>.azurecomm.net`, a `<guid>` odczytasz
w zasobie Email Service jako `mailFromSenderDomain`. Provider Terraforma nie ma źródła danych na domeny
poczty, więc to jedyna wartość, którą trzeba przepisać.

**Sprostowanie do wcześniejszej wersji tego pliku:** stało tu, że bez konfiguracji poczty serwer tożsamości
„nie wstanie", bo `SmtpOptions` ma `ValidateOnStart`. To była nieprawda — `ValidateOnStart()` bez żadnej
reguły walidacji niczego nie sprawdza, więc serwer wstawał, a wywalała się dopiero pierwsza rejestracja.
Reguła została dopisana i **teraz** to zdanie jest prawdziwe.

### Wdrożenie kodu

Terraform tworzy puste miejsca. Kod wgrywa się osobno:

- **Static Web Apps** — akcja GitHuba `Azure/static-web-apps-deploy`, token z wyjścia:

```bash
cd infra && terraform output -raw front_deployment_token
```

- **App Service** — `az webapp deploy` albo akcja `azure/webapps-deploy` z paczką z `dotnet publish`.

### Migracje bazy — tylko API, bo Identity robi to sam

Dwie bazy, dwa różne zachowania i łatwo je pomylić:

- **serwer tożsamości migruje się SAM przy starcie** (`OpenIddictSeeder.StartAsync` woła `MigrateAsync`),
  więc nie ma tu nic do zrobienia. ⚠️ Jego connection string musi mieć prawo do DDL — to nie może być
  rola ograniczona do odczytu i zapisu wierszy;
- **API nie migruje się samo** i ma się nie migrować: łączy się jako `budget_app`, rola bez prawa do DDL,
  i na tym polega podział ról pod RLS.

Migracja API jako krok wdrożenia, connection stringiem **właściciela** (nie tym, którym chodzi aplikacja):

```bash
cd api && dotnet ef migrations script --idempotent --project src/BudgetTracker.Api --output migracja.sql
```

Skrypt idempotentny daje się przejrzeć przed puszczeniem na prawdziwych danych — `database update` tego
nie daje. ⚠️ Role Postgresa zakłada osobno `api/db/setup-rls-roles.sql`, raz na klaster, **przed** migracją.

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
- **Wyłączenie kluczy do konta magazynu wymaga `storage_use_azuread = true` w providerze.** Bez tego
  `apply` pada na `403 Key based authentication is not permitted` już przy TWORZENIU konta, a nie dopiero
  przy kontenerze — provider zaraz po utworzeniu odpytuje warstwę danych. Do tego rola
  `Storage Blob Data Contributor` musi istnieć **przed** kontem, dlatego jest nadana na grupie zasobów:
  na samym koncie byłby cykl. Bycie właścicielem subskrypcji **nie** daje dostępu do danych w blobie.
