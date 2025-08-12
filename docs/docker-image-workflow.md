# Docker Image Build and Publish Workflow

This document describes the GitHub Actions workflow for building and publishing Docker images for the BBT Workflow execution and orchestrator services.

## Workflow Overview

The workflow `build-and-publish-images.yml` automatically builds and publishes Docker images when code is pushed to release branches.

### Triggers

The workflow runs on:

1. **Push to release branches**: `release-v*` (e.g., `release-v1.0`, `release-v2.0`)
2. **Manual dispatch**: Can be triggered manually with optional force publish flag

### Auto-Incrementing Versions

The workflow automatically calculates version numbers based on the branch name:

- **Branch Pattern**: `release-vX.Y` (e.g., `release-v1.0`, `release-v2.0`)
- **Version Generation**: Automatically finds the next available patch version
  - `release-v1.0` → `1.0.0`, `1.0.1`, `1.0.2`, etc.
  - `release-v2.0` → `2.0.0`, `2.0.1`, `2.0.2`, etc.

### Published Images

The workflow publishes two Docker images to GitHub Container Registry:

1. **Execution Service**: `ghcr.io/[owner]/[repo]/execution`
2. **Orchestrator Service**: `ghcr.io/[owner]/[repo]/orchestrator`

Each image is tagged with:
- Version number (e.g., `1.0.1`)
- `latest`
- Branch name
- Commit SHA

## Usage

### Automatic Deployment

1. Create a release branch:
   ```bash
   git checkout -b release-v1.0
   ```

2. Make your changes and push:
   ```bash
   git add .
   git commit -m "feat: new feature for v1.0"
   git push origin release-v1.0
   ```

3. The workflow will automatically:
   - Calculate the next version (e.g., `1.0.0`, `1.0.1`, etc.)
   - Build and test the solution
   - Create Docker images for both services
   - Push images to GitHub Container Registry
   - Create a Git tag and GitHub release
   - Update version in `common.props`
   - Generate security scans and SBOMs

### Manual Deployment

You can manually trigger the workflow from the GitHub Actions tab with the option to force publish images even if they already exist.

## Using the Published Images

### Docker Compose

```yaml
version: '3.8'
services:
  execution:
    image: ghcr.io/[owner]/[repo]/execution:1.0.1
    ports:
      - "5001:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production

  orchestrator:
    image: ghcr.io/[owner]/[repo]/orchestrator:1.0.1
    ports:
      - "5002:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    depends_on:
      - execution
```

### Kubernetes

```bash
# Deploy execution service
kubectl create deployment execution \
  --image=ghcr.io/[owner]/[repo]/execution:1.0.1

# Deploy orchestrator service  
kubectl create deployment orchestrator \
  --image=ghcr.io/[owner]/[repo]/orchestrator:1.0.1
```

### Docker CLI

```bash
# Pull images
docker pull ghcr.io/[owner]/[repo]/execution:1.0.1
docker pull ghcr.io/[owner]/[repo]/orchestrator:1.0.1

# Run execution service
docker run -d -p 5001:5000 \
  --name execution \
  ghcr.io/[owner]/[repo]/execution:1.0.1

# Run orchestrator service
docker run -d -p 5002:5000 \
  --name orchestrator \
  ghcr.io/[owner]/[repo]/orchestrator:1.0.1
```

## Security Features

The workflow includes several security features:

1. **Security Scanning**: Uses Anchore to scan images for vulnerabilities
2. **SBOM Generation**: Creates Software Bill of Materials for both images
3. **Multi-platform Builds**: Supports both AMD64 and ARM64 architectures
4. **Non-root User**: Images run as non-root user for security
5. **Minimal Base Images**: Uses Alpine Linux for smaller attack surface

## Workflow Features

- ✅ Auto-incrementing semantic versioning
- ✅ Multi-platform Docker builds (AMD64/ARM64)
- ✅ Parallel image building and pushing
- ✅ Comprehensive testing before image creation
- ✅ Security vulnerability scanning
- ✅ SBOM (Software Bill of Materials) generation
- ✅ GitHub Container Registry publishing
- ✅ Automatic Git tagging and release creation
- ✅ Docker Compose file updates
- ✅ Build caching for faster builds
- ✅ Detailed build summaries and notifications

## Configuration

### Environment Variables

The workflow uses these environment variables:

- `REGISTRY`: Container registry URL (default: `ghcr.io`)
- `EXECUTION_IMAGE_NAME`: Execution service image name
- `ORCHESTRATOR_IMAGE_NAME`: Orchestrator service image name

### Required Permissions

The workflow requires these GitHub permissions:

- `contents: write` - For creating tags and releases
- `packages: write` - For publishing to GitHub Container Registry
- `packages: read` - For accessing NuGet packages from burgan-tech organization

### Secrets

No additional secrets are required. The workflow uses the built-in `GITHUB_TOKEN` for:
- Publishing Docker images to GitHub Container Registry
- Accessing NuGet packages from burgan-tech GitHub Packages
- Creating releases and tags

### NuGet Package Dependencies

This project depends on BBT.Aether framework packages from the `burgan-tech` organization. The configuration works differently for different environments:

**Local Development:**
- Uses `nuget.config` file in repository root for persistent configuration
- Supports environment variables for GitHub credentials

**CI/CD Workflow:**
- Dynamically configures NuGet sources using `dotnet nuget add source` commands
- Uses GitHub Actions secrets for authentication

**Docker Builds:**
- Receives GitHub credentials as build arguments
- Configures NuGet sources during the build process
- No static config files copied into containers

## Troubleshooting

### Common Issues

1. **Version Already Exists**: The workflow will automatically increment to the next available patch version
2. **Build Failures**: Check the build logs for .NET compilation errors
3. **Test Failures**: The workflow continues even if tests fail but logs the results
4. **Push Failures**: Verify repository permissions for GitHub Container Registry
5. **NuGet Package Access Issues**: 
   - Ensure the `GITHUB_TOKEN` has `packages: read` permission
   - Verify the repository has access to `burgan-tech` organization packages
   - Check if the BBT.Aether packages exist in the burgan-tech GitHub Packages

### NuGet Package Troubleshooting

If you encounter issues accessing BBT.Aether packages:

**For Local Development:**
```bash
# Configure GitHub Packages source
dotnet nuget add source \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_PAT \
  --store-password-in-clear-text \
  --name burgan-tech-github \
  "https://nuget.pkg.github.com/burgan-tech/index.json"

# Test package access
dotnet nuget search BBT.Aether --source burgan-tech-github
```

**For CI/CD Issues:**
1. Check if the workflow has access to burgan-tech packages
2. Verify the `packages: read` permission in workflow permissions
3. Ensure BBT.Aether packages are published to burgan-tech GitHub Packages

### Manual Version Override

If you need to use a specific version, you can modify the `common.props` file before pushing:

```xml
<Version>1.0.5</Version>
```

The workflow will respect this version if the branch pattern doesn't match.

## Monitoring

The workflow provides comprehensive monitoring through:

- **GitHub Step Summary**: Detailed build information and next steps
- **Artifacts**: SBOM reports and test results
- **Security Alerts**: Vulnerability scan results in GitHub Security tab
- **Release Notes**: Automatic release creation with deployment instructions
