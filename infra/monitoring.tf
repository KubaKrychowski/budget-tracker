# Telemetria API: metryki, ślady i logi trafiają do Application Insights, który zapisuje je w workspace
# Log Analytics (tryb workspace-based — klasyczny jest wygaszany przez Microsoft).

resource "azurerm_log_analytics_workspace" "main" {
  name                = "${local.prefix}-logs"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = local.location

  sku               = "PerGB2018"
  retention_in_days = 30

  # ⚠️ Twardy limit dzienny: po jego przekroczeniu workspace przestaje przyjmować dane do końca doby
  # (UTC). Lepiej stracić telemetrię z reszty dnia niż dostać rachunek za pętlę spamującą logami.
  # 0,1 GB/dobę to ok. 3 GB miesięcznie, czyli mieści się w darmowej puli 5 GB.
  daily_quota_gb = var.log_daily_quota_gb

  tags = local.tags
}

resource "azurerm_application_insights" "main" {
  name                = "${local.prefix}-appi"
  resource_group_name = data.azurerm_resource_group.main.name
  location            = local.location

  workspace_id     = azurerm_log_analytics_workspace.main.id
  application_type = "web"

  tags = local.tags
}
