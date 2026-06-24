# StorageFlow Test Suites

StorageFlow separates tests by the kind of confidence they provide.

| Project | Purpose | External dependency |
|---|---|---|
| `StorageFlow.Tests.Unit` | Isolated algorithms and SDK request mapping | None |
| `StorageFlow.Tests.Component` | Fluent API and Core flows through the test-only InMemory provider | None |
| `StorageFlow.Tests.Integration` | Real MinIO, RustFS, LocalStack, and Redis contracts | Docker |
| `StorageFlow.Tests.Cloud` | Real AWS S3 smoke tests | AWS account |

## Fast tests

```bash
dotnet test tests/StorageFlow.Tests.Unit
dotnet test tests/StorageFlow.Tests.Component
```

These suites require no credentials, Docker, or network services and must run
with zero skipped tests.

## Docker integration tests

Install Docker Desktop or another Docker API-compatible runtime, then run:

```bash
STORAGEFLOW_TEST_DOCKER=true \
dotnet test tests/StorageFlow.Tests.Integration
```

Testcontainers starts pinned MinIO, RustFS, LocalStack, and Redis images on
random host ports. Credentials and buckets are generated only for the lifetime
of the test process. No local provider configuration or committed secret is
required. Without `STORAGEFLOW_TEST_DOCKER=true`, these tests are reported as
skipped and no container is started.

Pinned images:

| Service | Image |
|---|---|
| MinIO | `minio/minio:RELEASE.2025-09-07T16-13-09Z` |
| RustFS | `rustfs/rustfs:1.0.0-beta.7` |
| LocalStack | `localstack/localstack:4.14.0` |
| Redis | `redis:7.0` |

LocalStack `4.14.0` is the last token-free line supported by the selected
Testcontainers package. Newer LocalStack images require a private auth token,
which would violate this suite's no-user-credentials contract.

## AWS cloud tests

Use a dedicated existing bucket. The tests never create or delete the bucket;
they create objects under a unique `storageflow-tests/{run-id}/` prefix and
delete those objects during fixture cleanup.

Configure a local AWS CLI profile:

```bash
aws configure --profile storageflow-tests

export AWS_PROFILE=storageflow-tests
export STORAGEFLOW_TEST_AWS_ENABLED=true
export STORAGEFLOW_TEST_AWS_REGION=eu-north-1
export STORAGEFLOW_TEST_AWS_BUCKET=my-storageflow-test-bucket
export STORAGEFLOW_TEST_AWS_PREFIX=storageflow-tests

dotnet test tests/StorageFlow.Tests.Cloud
```

StorageFlow static credential options remain empty so the test exercises the
AWS SDK default credential chain. In CI, `.github/workflows/aws-smoke.yml` uses
GitHub OIDC instead of stored access keys. Configure the `aws-integration`
GitHub Environment with these variables:

```text
AWS_ROLE_ARN
AWS_REGION
AWS_TEST_BUCKET
```

The nightly workflow is opt-in. Also create this repository-level Actions
variable under **Settings > Secrets and variables > Actions > Variables**:

```text
AWS_SMOKE_ENABLED=true
```

Without that variable, scheduled AWS runs are skipped instead of producing a
failed deployment. A manual run can set `force_run` to validate the environment
configuration before enabling the nightly schedule.

GitHub records jobs that reference `environment: aws-integration` as
deployments. This is expected environment/audit behavior; the workflow does not
deploy an application.

The IAM role needs `s3:ListBucket` for the configured bucket and
`s3:GetObject`, `s3:PutObject`, and `s3:DeleteObject` under the
`storageflow-tests/*` prefix. Add a one-day lifecycle rule for that prefix as a
cleanup safety net.

No `.env`, local settings, AWS access key, or secret key belongs in the
repository.
