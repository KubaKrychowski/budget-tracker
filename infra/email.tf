# Poczta: Azure Communication Services przez SDK, nie przez przekaźnik SMTP.
#
# ⚠️ Terraform tych zasobów NIE zarządza — ACS, Email Service i domena stoją poza tym modułem i mają
# przeżyć każdy `destroy`. Tu jest wyłącznie ODCZYT, po to, żeby connection string nie musiał przechodzić
# przez ludzkie ręce: nikt go nie przepisuje, nie wkleja do pliku ani nie ustawia w zmiennej środowiskowej.
#
# Klucz dostępu ACS działa tylko tą drogą. Przekaźnik SMTP (smtp.azurecomm.net) wymagałby osobnego zasobu
# „SMTP Username" powiązanego z rejestracją aplikacji w Entra ID — trzech bytów do założenia ręcznie.
data "azurerm_communication_service" "email" {
  name                = var.communication_service_name
  resource_group_name = local.communication_service_resource_group
}
