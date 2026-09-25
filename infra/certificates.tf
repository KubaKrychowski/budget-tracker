# Certyfikaty OpenIddict. ⚠️ To NIE są certyfikaty TLS — te dla `*.azurewebsites.net` Azure daje sam i nic
# się o nie nie robi. Tu chodzi o materiał kryptograficzny APLIKACJI:
#
#   * podpisujący — kładzie podpis pod każdym wydanym JWT; API sprawdza go przez JWKS serwera tożsamości,
#   * szyfrujący  — chroni kody autoryzacyjne i refresh tokeny.
#
# Azure nie wie, że OpenIddict istnieje, więc nie ma czego wygenerować. Lokalnie robi to
# `AddDevelopmentSigningCertificate()`, ale na App Service to zawodzi cicho: klucz trafia do magazynu
# profilu użytkownika, który nie jest trwały, więc po restarcie powstaje NOWY — a wtedy wszystkie wydane
# tokeny stają się nieważne i wszyscy wylatują z sesji. Stąd generowanie tutaj.

resource "tls_private_key" "signing" {
  algorithm = "RSA"
  rsa_bits  = 2048
}

resource "tls_private_key" "encryption" {
  algorithm = "RSA"
  rsa_bits  = 2048
}

# ⚠️ Samopodpisany jest tu POPRAWNY, nie jest kompromisem. Nikt nie weryfikuje łańcucha zaufania tego
# certyfikatu — API bierze klucz publiczny z JWKS serwera tożsamości, a nie z urzędu certyfikacji.
# Certyfikat jest tylko opakowaniem na parę kluczy.
resource "tls_self_signed_cert" "signing" {
  private_key_pem = tls_private_key.signing.private_key_pem

  subject {
    common_name  = "${local.prefix}-identity-signing"
    organization = var.project
  }

  validity_period_hours = var.token_certificate_validity_hours

  # ⚠️ Zero, a nie wartość domyślna: przy niezerowym progu Terraform wymienia certyfikat SAM, przy
  # najbliższym `apply` po wejściu w okno odnowienia. Wymiana klucza podpisującego wylogowuje wszystkich,
  # więc ma być świadomą decyzją, a nie skutkiem ubocznym niepowiązanej zmiany w infrastrukturze.
  early_renewal_hours = 0

  allowed_uses = ["digital_signature"]
}

resource "tls_self_signed_cert" "encryption" {
  private_key_pem = tls_private_key.encryption.private_key_pem

  subject {
    common_name  = "${local.prefix}-identity-encryption"
    organization = var.project
  }

  validity_period_hours = var.token_certificate_validity_hours
  early_renewal_hours   = 0

  allowed_uses = ["key_encipherment", "data_encipherment"]
}
