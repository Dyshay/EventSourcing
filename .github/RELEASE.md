# Release Process

This document explains how to create a new release of the EventSourcing package.

## Manual Trigger (Recommended)

### Via GitHub Actions UI

1. Go to the **Actions** tab of your repository
2. Select the **Release** workflow from the sidebar
3. Click **Run workflow**
4. Fill in the parameters:
   - **Version**: The version to publish in semver format (e.g., `1.0.0`, `1.2.3`, `2.0.0-beta.1`)
   - **Mark as pre-release**: Check if this is a pre-release (alpha, beta, rc)
5. Click **Run workflow**

### What happens automatically

The workflow will:
1. ✅ Build the project
2. ✅ Run all tests (85 tests)
3. ✅ Create a Git tag `v{version}` (e.g., `v1.0.0`)
4. ✅ Create NuGet packages with the specified version
5. ✅ Publish packages to NuGet.org
6. ✅ Create a GitHub Release with:
   - Auto-generated release notes
   - Attached `.nupkg` files
   - Installation instructions

## Automatic Trigger (Alternative)

You can also manually create a tag to trigger the release:

```bash
git tag -a v1.0.0 -m "Release 1.0.0"
git push origin v1.0.0
```

## Version Format (Semantic Versioning)

Use [semver](https://semver.org/) format:

- **Patch** (`1.0.1`): Bug fixes, minor changes
- **Minor** (`1.1.0`): New backward-compatible features
- **Major** (`2.0.0`): Breaking changes
- **Pre-release** (`1.0.0-beta.1`): Test versions

### Examples

```
1.0.0          - First stable version
1.0.1          - Bug fixes
1.1.0          - New features
2.0.0          - Breaking changes
1.0.0-alpha.1  - Alpha pre-release
1.0.0-beta.1   - Beta pre-release
1.0.0-rc.1     - Release candidate
```

## Prerequisites

For the release to work, you must have configured:

1. **GitHub Secrets**:
   - `NUGET_API_KEY`: Your NuGet API key (obtained from nuget.org)
   - `GTB_TOKEN`: GitHub token with `contents: write` permission

2. **Permissions**:
   - The workflow automatically has `contents: write` to create tags and releases

## Post-Release Verification

1. Verify the release appears on GitHub: `https://github.com/{user}/{repo}/releases`
2. Verify packages are on NuGet: `https://www.nuget.org/packages/EventSourcing.MongoDB`
3. Test installation:
   ```bash
   dotnet add package EventSourcing.MongoDB --version {version}
   ```

## Troubleshooting

If the release fails:

1. Check the GitHub Actions workflow logs
2. Verify tests pass locally: `dotnet test`
3. Verify the version doesn't already exist on NuGet
4. Verify secrets are properly configured
5. If a tag was created by mistake, delete it:
   ```bash
   git tag -d v1.0.0
   git push origin :refs/tags/v1.0.0
   ```
