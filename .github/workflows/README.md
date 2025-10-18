# GitHub Actions Workflows

This directory contains GitHub Actions workflows for continuous integration and deployment.

## Workflows

### 1. CI Build and Test (`ci.yml`)

**Triggers:**
- Push to `master` or `main` branch
- Pull requests to `master` or `main` branch

**What it does:**
1. Sets up .NET 9.0
2. Starts MongoDB service (required for integration tests)
3. Restores NuGet dependencies
4. Builds the solution in Release mode
5. Runs all tests
6. Publishes test results as GitHub Actions annotations
7. Uploads test results as artifacts

**Status Badge:**
```markdown
![CI](https://github.com/YOUR_USERNAME/event-sourcing/workflows/CI%20Build%20and%20Test/badge.svg)
```

### 2. Code Coverage (`code-coverage.yml`)

**Triggers:**
- Push to `master` or `main` branch
- Pull requests to `master` or `main` branch

**What it does:**
1. Sets up .NET 9.0 and MongoDB
2. Builds the solution in Debug mode (for coverage instrumentation)
3. Runs tests with code coverage collection
4. Generates HTML coverage report using ReportGenerator
5. Uploads coverage to Codecov (optional)
6. Uploads coverage report as artifact

**Viewing Coverage:**
- Download the `coverage-report` artifact from the workflow run
- Open `index.html` in your browser
- Or view on Codecov if configured

**Status Badge:**
```markdown
[![codecov](https://codecov.io/gh/YOUR_USERNAME/event-sourcing/branch/master/graph/badge.svg)](https://codecov.io/gh/YOUR_USERNAME/event-sourcing)
```

### 3. Release (`release.yml`)

**Triggers:**
- Push tags matching `v*.*.*` (e.g., `v1.0.0`, `v2.1.3`)

**What it does:**
1. Extracts version number from the tag
2. Builds the solution
3. Runs tests to ensure quality
4. Packs NuGet packages with the version from the tag
5. Publishes packages to NuGet.org (requires `NUGET_API_KEY` secret)
6. Creates a GitHub Release with the packages attached

**Creating a Release:**

```bash
# Create and push a tag
git tag v1.0.0
git push origin v1.0.0
```

This will automatically:
- Build and test the code
- Create NuGet packages
- Publish to NuGet.org
- Create a GitHub Release

## Setup Instructions

### Required Secrets

Add these secrets to your GitHub repository (Settings → Secrets → Actions):

1. **NUGET_API_KEY** (required for release workflow)
   - Go to https://www.nuget.org/account/apikeys
   - Create a new API key with push permissions
   - Add as repository secret

2. **CODECOV_TOKEN** (optional, for code coverage)
   - Sign up at https://codecov.io
   - Add your repository
   - Copy the token
   - Add as repository secret

### MongoDB Service

The workflows use MongoDB as a service container for integration tests:
- Image: `mongo:7.0`
- Port: `27017`
- Health checks ensure MongoDB is ready before tests run

### Badges

Add these badges to your README.md:

```markdown
![CI](https://github.com/YOUR_USERNAME/event-sourcing/workflows/CI%20Build%20and%20Test/badge.svg)
[![codecov](https://codecov.io/gh/YOUR_USERNAME/event-sourcing/branch/master/graph/badge.svg)](https://codecov.io/gh/YOUR_USERNAME/event-sourcing)
```

## Local Testing

Test your workflows locally using [act](https://github.com/nektos/act):

```bash
# Install act
brew install act  # macOS
choco install act-cli  # Windows

# Run CI workflow
act push

# Run specific workflow
act -W .github/workflows/ci.yml
```

## Workflow Files

- `ci.yml` - Main CI pipeline (build + test)
- `code-coverage.yml` - Code coverage analysis
- `release.yml` - Release and publish to NuGet

## Customization

### Changing .NET Version

Update the `dotnet-version` in all workflows:

```yaml
- name: Setup .NET
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '9.0.x'  # Change this
```

### Changing MongoDB Version

Update the MongoDB service image:

```yaml
services:
  mongodb:
    image: mongo:7.0  # Change this
```

### Adding More Tests

The workflows automatically run all tests in the solution. Just add your tests to the `tests/` directory.

### Changing Build Configuration

Currently uses `Release` for CI and `Debug` for coverage. Modify as needed:

```yaml
- name: Build
  run: dotnet build EventSourcing.sln --configuration Release  # Change this
```

## Troubleshooting

### Tests fail in CI but pass locally

**Possible causes:**
1. MongoDB not available - check service container health
2. Timing issues - add delays or increase timeouts
3. Environment differences - check connection strings

**Solution:**
- Ensure tests use `mongodb://localhost:27017` connection string
- Add proper health checks to MongoDB service

### Release workflow fails to publish

**Possible causes:**
1. Missing `NUGET_API_KEY` secret
2. Invalid API key
3. Package version already exists

**Solution:**
- Verify API key is correct and has push permissions
- Check NuGet.org for existing package versions
- Use `--skip-duplicate` flag (already included)

### Coverage report not generated

**Possible causes:**
1. No code coverage data collected
2. ReportGenerator not installed
3. Wrong coverage file path

**Solution:**
- Ensure tests run with `--collect:"XPlat Code Coverage"`
- Check that coverage files exist: `./coverage/**/coverage.cobertura.xml`

## Further Reading

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [.NET CLI Reference](https://docs.microsoft.com/en-us/dotnet/core/tools/)
- [ReportGenerator Documentation](https://github.com/danielpalme/ReportGenerator)
- [Codecov Documentation](https://docs.codecov.com/)
