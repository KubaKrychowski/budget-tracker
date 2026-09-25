# Dwie osobne aplikacje, bo to dwa osobne buildy w tym samym workspace (`web/` i `web/projects/landing`)
# i dwa różne adresaty: aplikacja dla zalogowanego właściciela i strona dla kogoś, kto ocenia projekt.
#
# Plan Free: 100 GB transferu miesięcznie, 500 MB na aplikację, 2 własne domeny, certyfikat w cenie,
# limit 10 darmowych aplikacji na subskrypcję. Bundle landingu waży ok. 144 kB po kompresji, więc
# ograniczeniem nie jest tu miejsce.
#
# ⚠️ Plan Free NIE ma „linked backend" — SWA nie zestawi odwrotnego proxy do App Service. Front woła API
# bezpośrednio, między originami, czyli przez CORS. Dlatego adresy obu aplikacji trafiają do ustawień API.

resource "azurerm_static_web_app" "front" {
  name                = "${local.prefix}-front"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = var.static_web_apps_location

  sku_tier = "Free"
  sku_size = "Free"

  tags = local.tags
}

resource "azurerm_static_web_app" "landing" {
  name                = "${local.prefix}-landing"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = var.static_web_apps_location

  sku_tier = "Free"
  sku_size = "Free"

  tags = local.tags
}
