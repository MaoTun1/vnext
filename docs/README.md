# BBT Workflow Engine - Developer Documentation

## Overview

The BBT Workflow Engine is a comprehensive, domain-driven workflow management system built with .NET 8 and ASP.NET Core. It follows Clean Architecture principles and Domain-Driven Design (DDD) patterns to provide a scalable, maintainable, and extensible workflow orchestration platform.

## Table of Contents

- [Getting Started](./getting-started.md) - Setup and initial configuration
- [Architecture Overview](./architecture-overview.md) - System architecture and design patterns
- [Domain Models](./domain-models.md) - Core business entities and aggregates
- [Application Services](./application-services.md) - Application layer services and interfaces
- [Infrastructure Layer](./infrastructure-layer.md) - Data access and external integrations
- [Task Executors](./task-executors.md) - Task execution system and custom executors
- [Task Factory & Object Pooling](./task-factory-pooling.md) - High-performance task instance creation and memory optimization
- [Scripting Engine](./scripting-engine.md) - Script execution and compilation
- [Workflow Development Automation](./workflow-development-automation.md) - Automated workflow development tools and VS Code integration
- [Caching Strategy](./caching-strategy.md) - Caching implementation and patterns
- [Background Jobs](./background-jobs.md) - Asynchronous job processing
- [Multi-Schema Support](./multi-schema-support.md) - Multi-tenant schema management
- [Examples](./examples/) - Code examples and usage patterns

## Key Features

### Core Capabilities
- **Workflow Definition & Execution**: Define complex business workflows with states, transitions, and tasks
- **Hierarchical Workflows**: Support for sub-flows and sub-processes with parent-child relationships
- **Multi-Task Support**: HTTP, DAPR service calls, script execution, human tasks, and more
- **State Management**: Sophisticated state machine with conditional transitions
- **Script Engine**: Dynamic script execution for business logic and data transformation
- **Real-time Execution**: Synchronous and asynchronous workflow execution modes

### Workflow Orchestration
- **Sub-Flow Management**: Execute child workflows from parent workflows with correlation tracking
- **Blocking & Non-Blocking Execution**: SubFlows block parent execution, SubProcesses run in parallel
- **Instance Correlation**: Automatic tracking and management of parent-child workflow relationships
- **Cross-Schema Support**: Sub-flows can execute in different schemas and domains

### Technical Features
- **Clean Architecture**: Separation of concerns with Domain, Application, Infrastructure, and API layers
- **Multi-Schema Support**: Dynamic schema creation for multi-flow scenarios
- **Distributed Caching**: Redis-based caching with automatic invalidation
- **Background Processing**: DAPR-based job scheduling and execution
- **Health Monitoring**: Comprehensive health checks and telemetry
- **API Versioning**: RESTful APIs with version management

### Integration Capabilities
- **DAPR Integration**: Service invocation, pub/sub, bindings, and state management
- **Entity Framework Core**: Advanced ORM with multi-schema support
- **OpenTelemetry**: Distributed tracing and metrics collection
- **PostgreSQL**: Primary database with advanced JSON support

## Quick Start

```bash
# Clone the repository
git clone <repository-url>
cd workflowV2

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the application
./run-docker.sh 
```

## Technology Stack

- **.NET 9**: Latest .NET framework for high-performance applications
- **ASP.NET Core**: Web framework for building APIs
- **Entity Framework Core**: Object-relational mapping (ORM)
- **PostgreSQL**: Primary database with JSONB support
- **Redis**: Distributed caching and session storage
- **DAPR**: Distributed application runtime for microservices
- **OpenTelemetry**: Observability and monitoring
- **Docker**: Containerization and deployment

## Project Structure

```
BBT.Workflow/
├── orchestration/
|   └── BBT.Workflow.Orchestration.HttpApi.Host # Primary API for external clients
├── execution/
|   └── BBT.Workflow.Execution.HttpApi.Host # Internal API for task execution
├── src/
│   ├── BBT.Workflow.Domain/           # Domain models and business logic
│   ├── BBT.Workflow.Application/      # Application services and DTOs
│   ├── BBT.Workflow.Infrastructure/   # Data access and external services
|   ├── BBT.Workflow.HttpApi.Shared/   # Shared components and configurations for both API hosts
│   └── BBT.Workflow.HttpApi.Host/     # (LEGACY)
├── modules/
│   └── BBT.Workflow.Modules.Scripting/ # Scripting module
├── test/                              # Unit and integration tests
├── examples/                          # Usage examples
└── docs/                              # Documentation files
```

## Contributing

1. Read the architecture documentation to understand the system design
2. Follow the established patterns and conventions
3. Ensure all tests pass before submitting changes
4. Update documentation when adding new features

## Support

For technical questions and support:
- Review the documentation in this folder
- Check the examples directory for usage patterns
- Refer to the test projects for implementation details 