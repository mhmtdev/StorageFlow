# Security Policy

StorageFlow handles object keys, credentials, signed URLs, metadata, and
provider SDK responses. Security reports are taken seriously.

## Supported Versions

Security fixes are provided for the latest released minor version of the
current major release. Users should update to the latest available patch before
reporting an issue that may already be fixed.

| Version | Supported |
|---|---|
| Latest `1.x` release | Yes |
| Older releases | No |

Pre-release versions receive fixes on a best-effort basis until a stable
release is available.

## Reporting a Vulnerability

Do not disclose a suspected vulnerability in a public GitHub issue,
discussion, pull request, or test case.

Use GitHub's private vulnerability reporting flow:

1. Open the repository's **Security** tab.
2. Select **Advisories**.
3. Select **Report a vulnerability**.

Repository owners must enable GitHub Private Vulnerability Reporting for this
flow to be available. Until it is enabled, do not publish vulnerability details;
contact a maintainer privately through an established project communication
channel.

Include as much of the following information as possible:

- affected StorageFlow package and version;
- provider and provider version;
- deployment and runtime environment;
- vulnerability category and expected impact;
- minimal reproduction steps;
- proof-of-concept code or request, when safe to share;
- affected files or public APIs;
- suggested mitigation, if known;
- whether the issue is already public or actively exploited.

Never include real access keys, secret keys, session tokens, Redis credentials,
presigned URLs, or customer object data. Replace sensitive values with safe test
data.

## Response Process

Maintainers aim to acknowledge a complete report within five business days.
This is a target, not a service-level agreement. The report will be validated,
assigned a severity, and coordinated privately with the reporter.

When a vulnerability is confirmed, maintainers will work toward:

- a fix and regression test;
- a patched package release;
- a GitHub Security Advisory describing affected versions and mitigations;
- coordinated public disclosure after a fix is available.

Please allow time for investigation and remediation before public disclosure.

## Security Scope

Relevant reports include, but are not limited to:

- credential or signed URL disclosure;
- provider authentication or request-signing flaws;
- object-key validation or path traversal bypasses;
- header, metadata, or cache-key injection;
- unsafe file-signature validation behavior;
- cross-provider authorization or routing mistakes;
- vulnerabilities in StorageFlow's dependency usage;
- denial-of-service issues caused by unbounded memory, task, or stream usage.

Vulnerabilities in AWS S3, MinIO, RustFS, Redis, or their SDKs should also be
reported to the respective upstream project. If StorageFlow makes an upstream
issue exploitable or fails to apply an available mitigation, report it through
this private process as well.

## Operational Security

StorageFlow users remain responsible for provider IAM policies, bucket
policies, TLS, secret storage, CDN access controls, Redis security, dependency
updates, and object lifecycle configuration. Credentials must not be committed
to source control.
