output "resource_group_name" {
  description = "Resource group containing the deployment."
  value       = azurerm_resource_group.main.name
}

output "api_url" {
  description = "Public URL of the API."
  value       = "https://${azurerm_container_app.api.ingress[0].fqdn}"
}

output "web_url" {
  description = "Public URL of the web application."
  value       = "https://${azurerm_container_app.web.ingress[0].fqdn}"
}

output "key_vault_uri" {
  description = "Key Vault URI the workloads resolve secrets from."
  value       = azurerm_key_vault.main.vault_uri
}

output "postgres_fqdn" {
  description = "PostgreSQL host. Reachable only from the application subnet."
  value       = azurerm_postgresql_flexible_server.main.fqdn
}

output "application_insights_connection_string" {
  description = "Application Insights connection string."
  value       = azurerm_application_insights.main.connection_string
  sensitive   = true
}

# Deliberately not output: the PostgreSQL administrator password. It is written to Key Vault and
# read from there. Emitting it here would place it in Terraform state output and in any CI log that
# prints outputs.
