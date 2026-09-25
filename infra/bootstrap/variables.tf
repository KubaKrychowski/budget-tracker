variable "subscription_id" {
  description = "Identyfikator subskrypcji Azure."
  type        = string
}

variable "project" {
  description = "Przedrostek nazw zasobów. Tylko małe litery i cyfry — wchodzi w nazwę konta magazynu."
  type        = string
  default     = "wydatki"

  validation {
    condition     = can(regex("^[a-z0-9]{3,12}$", var.project))
    error_message = "Przedrostek musi mieć 3-12 znaków, wyłącznie małe litery i cyfry (ograniczenie nazw kont magazynu)."
  }
}

variable "location" {
  description = "Region zasobów stanu."
  type        = string
  default     = "westeurope"
}

variable "state_writer_principal_id" {
  description = <<-EOT
    Obiekt (użytkownik albo service principal), który będzie czytał i zapisywał stan.
    Własne konto: `az ad signed-in-user show --query id -o tsv`.
  EOT
  type        = string
}
