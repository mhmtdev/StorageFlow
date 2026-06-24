# StorageFlow Consumer Instructions

For StorageFlow-related changes, follow `docs/storageflow/STORAGEFLOW.md`.
Examples and failure guidance are in `docs/storageflow/examples.md` and
`docs/storageflow/troubleshooting.md`.

- Application code depends on `IStorageService`, not provider SDK clients.
- Use typed policies and `SFProvider`; string policy/provider selection is
  forbidden.
- Preserve streaming and caller ownership for upload/download streams.
- Use presigned URLs for temporary private access and delivery URLs for stable
  public CDN access.
- Handle `StorageResult` without leaking SDK exceptions.
- Keep credentials outside source control and logs.
- Respect the documented StorageFlow v1 boundaries.
