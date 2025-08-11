# Base image using Alpine
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app

# Create non-root user for security
RUN adduser -D workflowuser && chown -R workflowuser:workflowuser /app

USER workflowuser

# Build image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build

COPY ["common.props", "/common.props"]
COPY ["Directory.Build.props", "/Directory.Build.props"]
COPY ["global.json", "/global.json"]
COPY modules/ /modules

WORKDIR /src
COPY src/ .
RUN dotnet restore "BBT.Workflow.HttpApi.Host/BBT.Workflow.HttpApi.Host.csproj"
WORKDIR "/src/BBT.Workflow.HttpApi.Host"
ARG configuration=Release
RUN dotnet publish "BBT.Workflow.HttpApi.Host.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENV DOTNET_NUGET_SIGNATURE_VERIFICATION=false
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "BBT.Workflow.HttpApi.Host.dll"]