# StorageFlow Consumer Rules

The authoritative StorageFlow application contract is
`docs/storageflow/STORAGEFLOW.md`. Read it before changing registration,
policies, uploads, object operations, or URL generation. Consult the adjacent
examples and troubleshooting guides as needed.

Use `IStorageService`, typed policy keys, official provider tokens, streaming
I/O, and `StorageResult`. Do not bypass provider abstraction, trust raw client
provider selections, leak credentials or SDK exceptions, or invent out-of-scope
v1 features.
