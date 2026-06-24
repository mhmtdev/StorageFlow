# StorageFlow Consumer Rules

Read `docs/storageflow/STORAGEFLOW.md` before implementing StorageFlow changes.
Use `docs/storageflow/examples.md` and `docs/storageflow/troubleshooting.md` for
known patterns.

Application code must use `IStorageService`, typed policies, `SFProvider`, and
`StorageResult`. Preserve streaming behavior and stream ownership. Do not call
provider SDKs directly, select providers from client strings, store secrets in
the repository, or generate unsupported v1 behavior.
