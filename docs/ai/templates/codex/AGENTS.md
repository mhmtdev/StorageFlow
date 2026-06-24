# StorageFlow Consumer Rules

When changing StorageFlow application code, read and follow
`docs/storageflow/STORAGEFLOW.md` before implementation. Use
`docs/storageflow/examples.md` and `docs/storageflow/troubleshooting.md` as
supporting references.

Critical rules:

- Use `IStorageService`; do not call provider SDKs from application services.
- Use typed policy keys and `SFProvider` tokens; do not use client-supplied
  provider or policy strings.
- Keep upload and download streams streaming and respect caller stream
  ownership.
- Treat presigned URLs and public delivery URLs as different access models.
- Return and map `StorageResult` failures; do not leak SDK exceptions.
- Never put credentials in source, settings committed to Git, logs, or tests.
- Do not invent v1 features listed as out of scope in the canonical guide.
