variable "environment" {
  description = "Deployment environment. Drives sizing, redundancy and retention."
  type        = string

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "environment must be one of: dev, staging, prod."
  }
}

variable "location" {
  description = "Azure region. Also the data residency boundary for tenants pinned to this deployment."
  type        = string
  default     = "westeurope"
}

variable "project" {
  description = "Resource name prefix."
  type        = string
  default     = "optimizeall"
}

variable "postgres_administrator_login" {
  description = "PostgreSQL administrator login. Used only for migrations; the application connects as optimizeall_app."
  type        = string
  default     = "oa_admin"
}

variable "api_image" {
  description = "Fully qualified API container image, including tag."
  type        = string
}

variable "worker_image" {
  description = "Fully qualified worker container image, including tag."
  type        = string
}

variable "web_image" {
  description = "Fully qualified web container image, including tag."
  type        = string
}

variable "container_registry_server" {
  description = "Login server of the container registry holding the images."
  type        = string
}

variable "alert_email" {
  description = "Address that receives platform alerts."
  type        = string
}

variable "tags" {
  description = "Tags applied to every resource."
  type        = map(string)
  default     = {}
}
