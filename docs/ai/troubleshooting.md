# StorageFlow Agent Troubleshooting

## No provider is available

Confirm that the application installs Core plus an official provider package
and calls the matching `UseS3`, `UseMinio`, or `UseRustFs` registration method.

When several providers are registered, mark exactly one `.AsDefault()` or
select a provider on the operation/profile.

## A policy cannot be resolved

Confirm that the operation's marker class is the same type used during
registration. Do not replace marker types with strings. Resolution checks the
active provider override first, then global policies.

## Naming unexpectedly uses GUID

No explicit naming policy or global default was selected. Add `.AsDefault()` to
one global naming policy, or explicitly select another registered key with
`.Naming<TPolicyKey>()`.

Do not create a provider-level default naming policy; provider naming entries
only override a selected global key.

## Signature validation does not reject an unknown format

Built-in signatures cover `.jpg`, `.jpeg`, `.png`, `.pdf`, `.zip`, `.mp3`, and
`.mp4`. Unknown extensions skip the built-in signature step even when
`RequireValidSignature` is true. Implement and register `IFileValidator` for
additional binary formats.

## A non-seekable upload is truncated

Pass the original stream directly to `FromStream` and provide its length when
known. Do not inspect/read the stream in application code without replaying the
consumed prefix. StorageFlow's built-in validation preserves the bytes it
inspects.

## Download returns a stream instead of a URL

This is expected. `DownloadAsync` transfers object content through the
application. Use `GetPresignedUrlAsync<TPolicy>()` for temporary private direct
access or `GetDeliveryUrl<TPolicy>()` for stable public CDN URLs.

## Download fails after the method returns

Streaming provider errors can surface while reading `DownloadResult.Content`.
Do not dispose the stream before the consumer finishes. Map read failures at
the HTTP/application boundary.

## S3 credentials are missing

Leaving both static fields empty activates the AWS SDK default credential
chain. Supplying only access or only secret key is invalid. A session token
requires both static fields.

## MinIO or RustFS fails during startup

Both providers require explicit access and secret keys. Verify the endpoint,
scheme/SSL setting, region, path-style requirements, and secret configuration.
Do not print credentials in diagnostic output.

## Delivery URL is generated for a missing object

Delivery URL generation performs no existence check by design. It is a local
path transformation. Validate database references or object existence through
an appropriate application workflow when required.

## Presigned URLs are not cached

Install `StorageFlow.Extension.Redis` or register an `IDistributedCache` and
enable the presigned URL cache extension. Confirm the presigned policy is
selected. Redis has no role in delivery URLs.

## Retrying an upload fails

StorageFlow does not include retries. Before retrying, confirm the stream can be
replayed and reset it to the correct position. Prefer application-level
resilience that understands operation idempotency and stream ownership.
