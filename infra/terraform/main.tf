resource "azurerm_resource_group" "main" {
  name     = "rg-${local.name_prefix}"
  location = var.location
  tags     = local.common_tags
}

# ---------------------------------------------------------------------------
# Networking
# ---------------------------------------------------------------------------
# PostgreSQL and Redis are reachable only from the application subnet. Neither has a public
# endpoint: a database that is merely password-protected on the public internet is one credential
# leak away from a breach, and this one holds every tenant's data.

resource "azurerm_virtual_network" "main" {
  name                = "vnet-${local.name_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  address_space       = ["10.40.0.0/16"]
  tags                = local.common_tags
}

resource "azurerm_subnet" "apps" {
  name                 = "snet-apps"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name

  # Container Apps requires a /23 or larger for its infrastructure subnet.
  address_prefixes = ["10.40.0.0/23"]

  delegation {
    name = "container-apps"

    service_delegation {
      name    = "Microsoft.App/environments"
      actions = ["Microsoft.Network/virtualNetworks/subnets/join/action"]
    }
  }
}

resource "azurerm_subnet" "data" {
  name                 = "snet-data"
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.40.2.0/24"]

  service_endpoints = ["Microsoft.KeyVault"]

  delegation {
    name = "postgres"

    service_delegation {
      name    = "Microsoft.DBforPostgreSQL/flexibleServers"
      actions = ["Microsoft.Network/virtualNetworks/subnets/join/action"]
    }
  }
}

resource "azurerm_private_dns_zone" "postgres" {
  name                = "${local.name_prefix}.postgres.database.azure.com"
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.common_tags
}

resource "azurerm_private_dns_zone_virtual_network_link" "postgres" {
  name                  = "link-postgres"
  resource_group_name   = azurerm_resource_group.main.name
  private_dns_zone_name = azurerm_private_dns_zone.postgres.name
  virtual_network_id    = azurerm_virtual_network.main.id
  tags                  = local.common_tags
}

# ---------------------------------------------------------------------------
# Identity
# ---------------------------------------------------------------------------
# A user-assigned identity per workload. Workload identity means no secret is ever stored to reach
# Key Vault, and separate identities keep the blast radius of a compromised container to that
# container's own grants.

resource "azurerm_user_assigned_identity" "api" {
  name                = "id-${local.name_prefix}-api"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.common_tags
}

resource "azurerm_user_assigned_identity" "worker" {
  name                = "id-${local.name_prefix}-worker"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.common_tags
}

# ---------------------------------------------------------------------------
# Secrets
# ---------------------------------------------------------------------------

resource "azurerm_key_vault" "main" {
  name                = "kv-${substr(replace(local.name_prefix, "-", ""), 0, 20)}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"

  enable_rbac_authorization = true

  # A vault holding every tenant's provider credentials must be recoverable from a mistaken delete.
  # Purge protection cannot be turned off once enabled, which is the point.
  soft_delete_retention_days = 90
  purge_protection_enabled   = local.is_production

  public_network_access_enabled = false

  network_acls {
    bypass                     = "AzureServices"
    default_action             = "Deny"
    virtual_network_subnet_ids = [azurerm_subnet.apps.id, azurerm_subnet.data.id]
  }

  tags = local.common_tags
}

data "azurerm_client_config" "current" {}

# Read-only, and only on secrets. Neither workload needs to create, rotate or delete a secret, and
# granting more would let a compromised container exfiltrate the entire vault.
resource "azurerm_role_assignment" "api_secrets" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.api.principal_id
}

resource "azurerm_role_assignment" "worker_secrets" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.worker.principal_id
}

resource "random_password" "postgres_admin" {
  length           = 32
  special          = true
  override_special = "!#$%&*()-_=+[]{}"
}

resource "azurerm_key_vault_secret" "postgres_admin_password" {
  name         = "postgres-admin-password"
  value        = random_password.postgres_admin.result
  key_vault_id = azurerm_key_vault.main.id
  content_type = "password"
  tags         = local.common_tags
}
