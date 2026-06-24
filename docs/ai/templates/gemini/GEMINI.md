# StorageFlow Application Instructions

For every StorageFlow-related task, first read
`docs/storageflow/STORAGEFLOW.md`. Use `docs/storageflow/examples.md` for code
patterns and `docs/storageflow/troubleshooting.md` for known failures.

Keep application code provider agnostic through `IStorageService`. Use typed
policies, `SFProvider`, fluent operations, streaming I/O, and `StorageResult`.
Never commit credentials, expose arbitrary provider strings, leak SDK-specific
exceptions, or implement APIs outside the documented StorageFlow v1 scope.
