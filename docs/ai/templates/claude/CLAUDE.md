# StorageFlow Application Guidance

Before editing code that registers or calls StorageFlow, read
`docs/storageflow/STORAGEFLOW.md`. Consult `docs/storageflow/examples.md` for
approved patterns and `docs/storageflow/troubleshooting.md` before creating a
workaround.

Always use `IStorageService`, typed policy keys, official provider registration,
and `StorageResult`. Preserve stream ownership and streaming behavior. Never
select providers from arbitrary client strings, leak provider SDK exceptions,
commit credentials, or implement features marked outside StorageFlow v1.
