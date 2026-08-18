# ---------------------------------------------------------------------------
# PostgreSQL
# ---------------------------------------------------------------------------

resource "azurerm_postgresql_flexible_server" "main" {
  name                = "psql-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  version             = "16"

  administrator_login    = var.postgres_administrator_login
  administrator_password = random_password.postgres_admin.result

  sku_name   = local.postgres_sku
  storage_mb = local.postgres_storage_mb

  # 35 days in production. Long enough to recover from corruption discovered at a month-end close,
  # which is a realistic detection lag for a data-integrity problem.
  backup_retention_days        = local.postgres_backup_days
  geo_redundant_backup_enabled = local.postgres_geo_redundant

  delegated_subnet_id = azurerm_subnet.data.id
  private_dns_zone_id = azurerm_private_dns_zone.postgres.id

  public_network_access_enabled = false

  dynamic "high_availability" {
    for_each = local.postgres_high_available ? [1] : []

    content {
      # Zone-redundant rather than same-zone: a zone outage is the failure this is bought to survive.
      mode = "ZoneRedundant"
    }
  }

  maintenance_window {
    day_of_week  = 0
    start_hour   = 2
    start_minute = 0
  }

  tags = local.common_tags

  depends_on = [azurerm_private_dns_zone_virtual_network_link.postgres]
}

resource "azurerm_postgresql_flexible_server_database" "main" {
  name      = "optimizeall"
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"

  lifecycle {
    # Terraform destroying the database would take every tenant's data with it. Removing this
    # resource from state is a deliberate, manual act.
    prevent_destroy = true
  }
}

# pgvector and citext are required by the schema. Without them the migration fails on first apply,
# which is a confusing way to discover a missing extension.
resource "azurerm_postgresql_flexible_server_configuration" "extensions" {
  name      = "azure.extensions"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "VECTOR,CITEXT,PG_STAT_STATEMENTS"
}

resource "azurerm_postgresql_flexible_server_configuration" "log_min_duration" {
  name      = "log_min_duration_statement"
  server_id = azurerm_postgresql_flexible_server.main.id

  # Logs statements slower than a second. Fast enough to catch a regression, slow enough that the
  # log is not itself a performance problem.
  value = "1000"
}

resource "azurerm_postgresql_flexible_server_configuration" "connection_throttling" {
  name      = "connection_throttle.enable"
  server_id = azurerm_postgresql_flexible_server.main.id
  value     = "on"
}

# ---------------------------------------------------------------------------
# Redis
# ---------------------------------------------------------------------------

resource "azurerm_redis_cache" "main" {
  name                = "redis-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  capacity = local.redis_capacity
  family   = local.redis_family
  sku_name = local.redis_sku

  minimum_tls_version           = "1.2"
  non_ssl_port_enabled          = false
  public_network_access_enabled = false

  redis_configuration {
    # The run queue is a stream that must survive a restart. Eviction of queued work would silently
    # drop agent runs a human had already approved.
    maxmemory_policy = "noeviction"
  }

  tags = local.common_tags
}

resource "azurerm_key_vault_secret" "redis_connection" {
  name         = "redis-connection-string"
  value        = "${azurerm_redis_cache.main.hostname}:${azurerm_redis_cache.main.ssl_port},password=${azurerm_redis_cache.main.primary_access_key},ssl=True,abortConnect=False"
  key_vault_id = azurerm_key_vault.main.id
  content_type = "connection-string"
  tags         = local.common_tags
}

# ---------------------------------------------------------------------------
# Artefact storage
# ---------------------------------------------------------------------------

resource "azurerm_storage_account" "artifacts" {
  name                = substr(replace("st${local.name_prefix}", "-", ""), 0, 24)
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  account_tier             = "Standard"
  account_replication_type = local.is_production ? "GZRS" : "LRS"

  https_traffic_only_enabled      = true
  min_tls_version                 = "TLS1_2"
  allow_nested_items_to_be_public = false
  shared_access_key_enabled       = false

  blob_properties {
    versioning_enabled = true

    delete_retention_policy {
      days = 30
    }

    container_delete_retention_policy {
      days = 30
    }
  }

  tags = local.common_tags
}

resource "azurerm_storage_container" "exports" {
  name                  = "exports"
  storage_account_id    = azurerm_storage_account.artifacts.id
  container_access_type = "private"
}
