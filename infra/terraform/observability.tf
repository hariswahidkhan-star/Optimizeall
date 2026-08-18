resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = local.log_retention_days
  tags                = local.common_tags
}

resource "azurerm_application_insights" "main" {
  name                = "appi-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = local.common_tags
}

resource "azurerm_monitor_action_group" "oncall" {
  name                = "ag-${local.name_prefix}-oncall"
  resource_group_name = azurerm_resource_group.main.name
  short_name          = "oncall"

  email_receiver {
    name                    = "oncall-email"
    email_address           = var.alert_email
    use_common_alert_schema = true
  }

  tags = local.common_tags
}

# Alerts are deliberately few and each one is actionable. A dashboard full of alerts nobody acts on
# trains an on-call engineer to ignore the one that matters.

resource "azurerm_monitor_metric_alert" "api_failures" {
  name                = "alert-${local.name_prefix}-api-5xx"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_application_insights.main.id]
  description         = "The API is returning server errors. Users are seeing failures now."
  severity            = 1
  frequency           = "PT1M"
  window_size         = "PT5M"

  criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "requests/failed"
    aggregation      = "Count"
    operator         = "GreaterThan"
    threshold        = 10
  }

  action {
    action_group_id = azurerm_monitor_action_group.oncall.id
  }

  tags = local.common_tags
}

resource "azurerm_monitor_metric_alert" "postgres_storage" {
  name                = "alert-${local.name_prefix}-postgres-storage"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_postgresql_flexible_server.main.id]

  # Fires with room to act. A database that reaches 100% stops accepting writes, and by then the
  # options are all bad ones.
  description = "PostgreSQL storage is above 80%. Expand before writes start failing."
  severity    = 2
  frequency   = "PT5M"
  window_size = "PT15M"

  criteria {
    metric_namespace = "Microsoft.DBforPostgreSQL/flexibleServers"
    metric_name      = "storage_percent"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 80
  }

  action {
    action_group_id = azurerm_monitor_action_group.oncall.id
  }

  tags = local.common_tags
}

resource "azurerm_monitor_scheduled_query_rules_alert_v2" "audit_chain_broken" {
  name                = "alert-${local.name_prefix}-audit-chain"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  # The highest-severity alert in the platform. A broken audit chain means the tamper-evidence
  # guarantee has failed, and every governance claim downstream of it is in question.
  description = "The audit hash chain failed verification. Treat as a potential integrity incident."
  severity    = 0

  evaluation_frequency = "PT15M"
  window_duration      = "PT1H"
  scopes               = [azurerm_log_analytics_workspace.main.id]

  criteria {
    query                   = <<-QUERY
      AppTraces
      | where Message has "Audit chain broken"
      | summarize Count = count()
    QUERY
    time_aggregation_method = "Count"
    threshold               = 0
    operator                = "GreaterThan"
  }

  action {
    action_groups = [azurerm_monitor_action_group.oncall.id]
  }

  tags = local.common_tags
}

resource "azurerm_monitor_scheduled_query_rules_alert_v2" "approval_bypass_attempt" {
  name                = "alert-${local.name_prefix}-approval-bypass"
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location

  description = "An execution was refused because the payload differed from what was approved. Investigate immediately."
  severity    = 0

  evaluation_frequency = "PT5M"
  window_duration      = "PT15M"
  scopes               = [azurerm_log_analytics_workspace.main.id]

  criteria {
    query                   = <<-QUERY
      AppTraces
      | where Message has "approval.payload_mismatch"
      | summarize Count = count()
    QUERY
    time_aggregation_method = "Count"
    threshold               = 0
    operator                = "GreaterThan"
  }

  action {
    action_groups = [azurerm_monitor_action_group.oncall.id]
  }

  tags = local.common_tags
}
