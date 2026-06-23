# Releasing StorageFlow

StorageFlow packages are published from GitHub Actions through NuGet.org
Trusted Publishing. Do not store a long-lived NuGet API key in the repository,
GitHub secrets, local configuration, or shell history.

## One-time NuGet.org setup

In the `mhmtdev` NuGet.org account, open **Trusted Publishing** and create a
GitHub Actions policy with these values:

| Field | Value |
|---|---|
| Policy owner | `mhmtdev` (individual) |
| Repository owner | `mhmtdev` |
| Repository | `StorageFlow` |
| Workflow file | `publish.yml` |
| Environment | Leave empty |

The workflow requests a short-lived OIDC credential through `NuGet/login@v1`.
It never receives or stores the NuGet.org account password.

## Release process

1. Confirm the `main` branch CI workflow is green.
2. Confirm the version is unused on NuGet.org.
3. Create an annotated SemVer tag on a commit contained in `main`.
4. Push only that tag.

For the first release candidate:

```bash
git switch main
git pull --ff-only
git tag -a v1.0.0-rc.1 -m "StorageFlow 1.0.0-rc.1"
git push origin v1.0.0-rc.1
```

`.github/workflows/publish.yml` then:

- verifies the tag belongs to `main`;
- runs a zero-warning Release build;
- runs Unit, Component, and Docker Integration suites with zero skips;
- creates six `.nupkg` and six `.snupkg` artifacts;
- checks production package boundaries;
- publishes through NuGet.org Trusted Publishing;
- creates a GitHub prerelease or stable release for the tag.

After NuGet.org finishes package validation, install the packages into a clean
consumer project and run the Advanced Sample against the published feed.

## Stable release

Publish `v1.0.0` only after the release candidate has completed consumer and
provider smoke testing without a release-blocking API change:

```bash
git tag -a v1.0.0 -m "StorageFlow 1.0.0"
git push origin v1.0.0
```

NuGet package versions are immutable. Never delete and reuse a version; publish
a new patch or prerelease version instead.
