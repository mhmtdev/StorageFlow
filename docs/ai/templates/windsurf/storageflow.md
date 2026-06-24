# StorageFlow Consumer Rules

Use `docs/storageflow/STORAGEFLOW.md` as the authoritative guide when editing
StorageFlow application code. Supporting material is available in
`docs/storageflow/examples.md` and `docs/storageflow/troubleshooting.md`.

Generate code through `IStorageService`, typed policies, fluent operations, and
official `SFProvider` tokens. Preserve streaming and caller-owned streams. Do
not leak SDK exceptions or credentials, accept raw provider strings from
clients, or assume features outside StorageFlow v1.
