resource "azurerm_container_app_environment" "main" {
  name                = "cae-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  log_analytics_workspace_id     = azurerm_log_analytics_workspace.main.id
  infrastructure_subnet_id       = azurerm_subnet.apps.id
  internal_load_balancer_enabled = false

  # Zone redundancy in production only. It requires a larger subnet and costs more, and a
  # development environment does not need to survive a zone outage.
  zone_redundancy_enabled = local.is_production

  tags = local.common_tags
}

resource "azurerm_container_app" "api" {
  name                         = "ca-${local.name_prefix}-api"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.api.id]
  }

  registry {
    server   = var.container_registry_server
    identity = azurerm_user_assigned_identity.api.id
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "http"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = local.api_min_replicas
    max_replicas = local.api_max_replicas

    container {
      name   = "api"
      image  = var.api_image
      cpu    = local.is_production ? 1.0 : 0.5
      memory = local.is_production ? "2Gi" : "1Gi"

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = local.is_production ? "Production" : title(var.environment)
      }

      env {
        name  = "Infrastructure__KeyVaultUri"
        value = azurerm_key_vault.main.vault_uri
      }

      # The identity's client id, so the container's Azure credential picks the right user-assigned
      # identity when several are attached.
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.api.client_id
      }

      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = azurerm_application_insights.main.connection_string
      }

      liveness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/health/live"

        # Liveness must not depend on the database. A restart cannot fix a database outage, and
        # conflating the two turns a dependency incident into a restart storm.
        initial_delay           = 15
        interval_seconds        = 30
        failure_count_threshold = 3
      }

      readiness_probe {
        transport               = "HTTP"
        port                    = 8080
        path                    = "/health/ready"
        interval_seconds        = 10
        failure_count_threshold = 3
      }
    }

    http_scale_rule {
      name                = "http-concurrency"
      concurrent_requests = "50"
    }
  }

  tags = local.common_tags
}

resource "azurerm_container_app" "worker" {
  name                         = "ca-${local.name_prefix}-worker"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.worker.id]
  }

  registry {
    server   = var.container_registry_server
    identity = azurerm_user_assigned_identity.worker.id
  }

  # No ingress. The worker takes work from the queue; nothing should be able to reach it directly.

  template {
    min_replicas = local.worker_min_replicas
    max_replicas = local.worker_max_replicas

    container {
      name   = "worker"
      image  = var.worker_image
      cpu    = local.is_production ? 1.0 : 0.5
      memory = local.is_production ? "2Gi" : "1Gi"

      env {
        name  = "DOTNET_ENVIRONMENT"
        value = local.is_production ? "Production" : title(var.environment)
      }

      env {
        name  = "Infrastructure__KeyVaultUri"
        value = azurerm_key_vault.main.vault_uri
      }

      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.worker.client_id
      }

      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = azurerm_application_insights.main.connection_string
      }
    }

    # Scales on queue depth rather than CPU. An agent run spends most of its wall clock waiting on a
    # provider, so CPU stays low while the backlog grows — the opposite of what CPU-based scaling
    # would conclude.
    custom_scale_rule {
      name             = "queue-depth"
      custom_rule_type = "redis"

      metadata = {
        address       = "${azurerm_redis_cache.main.hostname}:${azurerm_redis_cache.main.ssl_port}"
        listName      = "optimizeall:runs"
        listLength    = "5"
        enableTLS     = "true"
        databaseIndex = "0"
      }

      authentication {
        secret_name       = "redis-password"
        trigger_parameter = "password"
      }
    }
  }

  secret {
    name                = "redis-password"
    key_vault_secret_id = azurerm_key_vault_secret.redis_connection.id
    identity            = azurerm_user_assigned_identity.worker.id
  }

  tags = local.common_tags
}

resource "azurerm_container_app" "web" {
  name                         = "ca-${local.name_prefix}-web"
  resource_group_name          = azurerm_resource_group.main.name
  container_app_environment_id = azurerm_container_app_environment.main.id
  revision_mode                = "Single"

  registry {
    server   = var.container_registry_server
    identity = azurerm_user_assigned_identity.api.id
  }

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.api.id]
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "http"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  template {
    min_replicas = local.is_production ? 2 : 0
    max_replicas = local.is_production ? 6 : 2

    container {
      name   = "web"
      image  = var.web_image
      cpu    = 0.25
      memory = "0.5Gi"

      liveness_probe {
        transport = "HTTP"
        port      = 8080
        path      = "/healthz"
      }
    }
  }

  tags = local.common_tags
}
