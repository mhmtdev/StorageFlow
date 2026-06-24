---
applyTo: "**/*.cs,**/*.csproj,**/appsettings*.json"
---

# StorageFlow Application Rules

Read `docs/storageflow/STORAGEFLOW.md` for the complete consumer contract.

Use `IStorageService`, typed policies, fluent operations, and official
`SFProvider` tokens. Do not bypass StorageFlow with SDK clients, accept raw
provider/policy strings from clients, buffer whole objects without an explicit
application requirement, leak credentials, or assume out-of-scope v1 features.
