terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.14"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # State holds resource identifiers and, unavoidably, some sensitive values. It lives in a storage
  # account with versioning and soft delete, never on a developer's disk.
  backend "azurerm" {}
}

provider "azurerm" {
  features {
    key_vault {
      # Soft delete is left in place on destroy. A vault purged by a mistaken `terraform destroy`
      # takes every secret with it, and recovery is impossible rather than merely painful.
      purge_soft_delete_on_destroy       = false
      recover_soft_deleted_key_vaults    = true
    }

    resource_group {
      prevent_deletion_if_contains_resources = true
    }
  }
}
