# Contributing to StorageFlow

Thank you for helping improve StorageFlow. Contributions may include bug
reports, design feedback, documentation, tests, and code.

## Before You Start

- Search existing issues before opening a new one.
- Use a public issue for bugs, feature proposals, and design discussions.
- Use the private process in [SECURITY.md](SECURITY.md) for suspected security
  vulnerabilities.
- Small documentation corrections and focused bug fixes may be submitted
  directly.
- Discuss substantial features, public API changes, new dependencies, and
  provider behavior changes in an issue before implementation.

StorageFlow deliberately keeps a narrow object-storage scope. A proposal may
be better implemented as an extension package rather than added to Core.

## Development Requirements

- .NET 9 SDK
- Docker-compatible runtime for integration tests
- An AWS account only for opt-in cloud smoke tests

Restore and build the solution:

```bash
dotnet restore StorageFlow.sln
dotnet build StorageFlow.sln -c Release --no-restore
```

## Repository Structure

```text
src/StorageFlow.Abstractions   Public contracts and result models
src/StorageFlow.Core           Fluent API, pipeline, policies, and profiles
src/StorageFlow.Provider.*     First-party provider implementations
src/StorageFlow.Extension.*    Optional framework extensions
tests/StorageFlow.Tests.Unit   Isolated unit tests
tests/StorageFlow.Tests.Component
                               Core flows using the test-only provider
tests/StorageFlow.Tests.Integration
                               Real Docker provider and Redis tests
tests/StorageFlow.Tests.Cloud  Opt-in AWS S3 smoke tests
```

Package dependency boundaries are strict:

- `StorageFlow.Abstractions` has no project dependencies.
- `StorageFlow.Core` depends only on Abstractions.
- Provider packages depend only on Abstractions, never Core.
- Extension packages depend only on Abstractions, never Core or a provider.
- `StorageFlow.Testing` must not be referenced by production or sample
  projects.

## Coding Guidelines

- Preserve nullable reference type correctness.
- Add XML documentation to every public member.
- Keep public abstractions mockable; do not introduce static service locators.
- Keep provider-specific SDK types out of Core and application-facing APIs.
- Return `StorageResult` from public storage operations instead of leaking SDK
  exceptions.
- Use strongly typed policy keys; do not add string-based public policy APIs.
- Preserve the upload pipeline order: validation, naming, provider upload, and
  optional presigned URL cache warm-up.
- Do not dispose streams owned by callers.
- Keep changes focused. Avoid unrelated formatting or refactoring.

Read [AGENTS.md](AGENTS.md) and
`.github/instructions/storageflow-architecture.instructions.md` before changing
architecture or public behavior.

## Tests

Unit and Component tests must pass without Docker, network services, or
credentials:

```bash
dotnet test tests/StorageFlow.Tests.Unit
dotnet test tests/StorageFlow.Tests.Component
```

Run real provider and Redis contracts with Docker:

```bash
STORAGEFLOW_TEST_DOCKER=true \
dotnet test tests/StorageFlow.Tests.Integration
```

AWS cloud tests are opt-in and require a dedicated existing bucket. See
[tests/README.md](tests/README.md) for environment variables, IAM permissions,
and cleanup behavior.

Add or update tests at the narrowest appropriate level:

- Unit tests for algorithms, validators, naming, SDK request mapping, and error
  mapping.
- Component tests for fluent API, policies, profiles, routing, and cache flows.
- Integration tests for behavior that requires a real compatible service.
- Cloud tests only for behavior that cannot be represented reliably by an
  emulator.

## Pull Requests

A pull request should:

- explain the problem and the chosen solution;
- reference the related issue when one exists;
- include tests for changed behavior;
- update public documentation for API or behavior changes;
- keep breaking changes explicit;
- pass Release build, Unit tests, Component tests, and applicable Integration
  tests;
- contain no credentials, local settings, generated artifacts, or unrelated
  changes.

Maintainers may request changes to preserve package boundaries, API
consistency, performance, or V1 scope. Review feedback should be resolved before
merge.

## License

By contributing, you agree that your contributions will be licensed under the
[MIT License](LICENSE).
