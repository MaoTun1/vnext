# Base image using Alpine
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app

# Create non-root user for security
RUN adduser -D workflowuser && chown -R workflowuser:workflowuser /app

USER workflowuser

# Build image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

# Build arguments for GitHub packages access
ARG GITHUB_USERNAME
ARG GITHUB_TOKEN

COPY ["common.props", "/common.props"]
COPY ["Directory.Build.props", "/Directory.Build.props"]
COPY ["global.json", "/global.json"]
COPY ["nuget.config", "/nuget.config"]
COPY modules/ /modules
COPY src/ /src

# Configure GitHub NuGet credentials if provided
RUN if [ ! -z "$GITHUB_USERNAME" ] && [ ! -z "$GITHUB_TOKEN" ]; then \
      dotnet nuget update source burgan-tech-github \
        --username "$GITHUB_USERNAME" \
        --password "$GITHUB_TOKEN" \
        --store-password-in-clear-text \
        --configfile /nuget.config \
        || echo "Could not update source credentials"; \
    fi

WORKDIR /orchestration
COPY orchestration/ .
RUN dotnet restore "BBT.Workflow.Orchestration.HttpApi.Host/BBT.Workflow.Orchestration.HttpApi.Host.csproj"
WORKDIR "/orchestration/BBT.Workflow.Orchestration.HttpApi.Host"
ARG configuration=Release
RUN dotnet publish "BBT.Workflow.Orchestration.HttpApi.Host.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENV DOTNET_NUGET_SIGNATURE_VERIFICATION=false
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "BBT.Workflow.Orchestration.HttpApi.Host.dll"] 