locals {
  name_prefix = "${var.project}-${var.environment}"

  is_production = var.environment == "prod"

  common_tags = merge(
    {
      project     = var.project
      environment = var.environment
      managed_by  = "terraform"
      component   = "enterprise-ai-os"
    },
    var.tags,
  )

  # Production runs zone-redundant with a warm floor of replicas; non-production scales to zero,
  # because an idle development environment should cost nothing.
  api_min_replicas    = local.is_production ? 2 : 0
  api_max_replicas    = local.is_production ? 20 : 3
  worker_min_replicas = local.is_production ? 2 : 0
  worker_max_replicas = local.is_production ? 30 : 3

  postgres_sku            = local.is_production ? "GP_Standard_D4ds_v5" : "B_Standard_B2s"
  postgres_storage_mb     = local.is_production ? 262144 : 32768
  postgres_backup_days    = local.is_production ? 35 : 7
  postgres_geo_redundant  = local.is_production
  postgres_high_available = local.is_production

  redis_sku      = local.is_production ? "Premium" : "Basic"
  redis_family   = local.is_production ? "P" : "C"
  redis_capacity = local.is_production ? 1 : 0

  log_retention_days = local.is_production ? 90 : 30
}
