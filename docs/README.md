# BBT Workflow Engine - Developer Documentation

## Overview

The BBT Workflow Engine is a comprehensive, domain-driven workflow management system built with .NET 8 and ASP.NET Core. It follows Clean Architecture principles and Domain-Driven Design (DDD) patterns to provide a scalable, maintainable, and extensible workflow orchestration platform.

## Table of Contents

### Getting Started
- [Getting Started](./getting-started.md) - Setup and initial configuration

### Architecture & Design
- [Architecture Overview](./architecture-overview.md) - System architecture, microservices design, and transition pipeline
- [Transition Pipeline Architecture](./transition-pipeline-architecture.md) - Deterministic transition lifecycle execution system
- [Domain Models](./domain-models.md) - Core business entities and aggregates
- [Strategy Pattern Implementation](./strategy-pattern-implementation.md) - Strategy pattern usage across the system

### Application Layer
- [Application Services](./application-services.md) - Application layer services and interfaces
- [Task Executors](./task-executors.md) - Task execution system and custom executors
- [Trigger Transition Tasks](./trigger-transition-tasks.md) - Workflow instance interaction tasks (Start, DirectTrigger, GetInstanceData, SubProcess)
- [Task Factory & Object Pooling](./task-factory-pooling.md) - High-performance task instance creation and memory optimization

### Scripting & Extensibility
- [Scripting Engine](./scripting-engine.md) - Dynamic C# script execution, logging, and configuration access
- [Workflow Development Automation](./workflow-development-automation.md) - Automated workflow development tools and VS Code integration

### Infrastructure & Integration
- [Infrastructure Layer](./infrastructure-layer.md) - Data access and external integrations
- [Multi-Schema Support](./multi-schema-support.md) - Multi-tenant schema management
- [ClickHouse Integration](./clickhouse-integration.md) - High-performance analytics with ClickHouse

### Background Processing
- [Background Jobs](./background-jobs.md) - Asynchronous job processing with Dapr
- [Timer Execution Enhancement](./timer-execution-enhancement.md) - Enhanced timer execution and scheduling

### Performance & Optimization
- [Caching Strategy](./caching-strategy.md) - Distributed caching implementation and patterns
- [Cache Metrics](./cache-metrics.md) - Cache performance monitoring
- [Persistent Metrics](./persistent-metrics.md) - Long-term metrics storage
- [Database Metrics Interceptor](./database-metrics-interceptor.md) - Database performance monitoring

### Observability & Monitoring
- [OpenTelemetry Logging](./opentelemetry-logging.md) - Distributed tracing, structured logging, and telemetry

### Data & Filtering
- [Instance Filtering](./instance-filtering.md) - Advanced filtering capabilities for workflow instances

### Examples
- [Examples](./examples/) - Code examples and usage patterns
  - [Dapr-Compatible Timer Examples](./examples/dapr-compatible-timer-examples.md)
  - [Enhanced Timer Mapping Examples](./examples/enhanced-timer-mapping-examples.md)

## Key Features

### Core Capabilities
- **Workflow Definition & Execution**: Define complex business workflows with states, transitions, and tasks
- **Hierarchical Workflows**: Support for sub-flows and sub-processes with parent-child relationships
- **Multi-Task Support**: HTTP, DAPR service calls, script execution, human tasks, and more
- **State Management**: Sophisticated state machine with conditional transitions
- **Script Engine**: Dynamic script execution for business logic and data transformation
- **Real-time Execution**: Synchronous and asynchronous workflow execution modes

### Workflow Orchestration
- **Transition Pipeline**: Deterministic, extensible pipeline-based transition execution
- **Dynamic Step Planning**: Runtime-configurable execution flow with directives
- **Trigger Handlers**: Specialized processing for Manual, Automatic, Scheduled, and Event-based transitions
- **Re-entry System**: Optimized handling of automatic and scheduled transitions
- **Sub-Flow Management**: Execute child workflows from parent workflows with correlation tracking
- **Blocking & Non-Blocking Execution**: SubFlows block parent execution, SubProcesses run in parallel
- **Instance Correlation**: Automatic tracking and management of parent-child workflow relationships
- **Cross-Schema Support**: Sub-flows can execute in different schemas and domains

### Technical Features
- **Clean Architecture**: Separation of concerns with Domain, Application, Infrastructure, and API layers
- **Microservices Architecture**: Separate Orchestration and Execution APIs for scalability
- **Transition Pipeline**: Step-based, deterministic workflow state management with dynamic planning
- **Multi-Schema Support**: Dynamic schema creation for multi-flow scenarios
- **Distributed Caching**: Redis-based caching with automatic invalidation
- **Background Processing**: Dapr-based job scheduling and execution
- **OpenTelemetry Integration**: Comprehensive distributed tracing and structured logging
- **Script Logging & Configuration**: Built-in logging and configuration access in scripts
- **Health Monitoring**: Comprehensive health checks and telemetry
- **API Versioning**: RESTful APIs with version management

### Integration Capabilities
- **Dapr Integration**: Service invocation, pub/sub, bindings, state management, and secrets
- **Entity Framework Core**: Advanced ORM with multi-schema support and performance interceptors
- **OpenTelemetry**: Distributed tracing, structured logging, and metrics collection with custom spans and events
- **PostgreSQL**: Primary database with advanced JSONB support and optimized queries
- **ClickHouse**: High-performance columnar database for analytics and metrics
- **Redis**: Distributed caching and session management

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
- **ASP.NET Core**: Web framework for building RESTful APIs
- **Entity Framework Core**: Object-relational mapping with multi-schema support
- **PostgreSQL**: Primary database with advanced JSONB support
- **ClickHouse**: High-performance columnar database for analytics
- **Redis**: Distributed caching and session management
- **Dapr**: Distributed application runtime for microservices (service invocation, pub/sub, secrets, state management)
- **OpenTelemetry**: Distributed tracing, structured logging, and metrics
- **Docker**: Containerization and orchestration
- **Roslyn**: Dynamic C# script compilation and execution

## Project Structure

```
vnext/
├── orchestration/
│   └── BBT.Workflow.Orchestration.HttpApi.Host/  # Public-facing API for workflow management
├── execution/
│   └── BBT.Workflow.Execution.HttpApi.Host/      # Internal API for task execution
├── src/
│   ├── BBT.Workflow.Domain/                      # Domain models and business logic
│   ├── BBT.Workflow.Application/                 # Application services, DTOs, and pipeline
│   ├── BBT.Workflow.Infrastructure/              # Data access, EF Core, and external integrations
│   └── BBT.Workflow.HttpApi.Shared/              # Shared middleware, telemetry, and utilities
├── modules/
│   └── BBT.Workflow.Modules.Scripting/           # Roslyn-based scripting module
├── test/
│   ├── BBT.Workflow.Domain.Tests/
│   ├── BBT.Workflow.Application.Tests/
│   └── BBT.Workflow.Infrastructure.Tests/
├── etc/
│   ├── docker/                                   # Docker Compose configuration
│   ├── execution/dapr/                           # Dapr configuration for Execution API
│   └── orchestration/dapr/                       # Dapr configuration for Orchestration API
└── docs/                                         # Technical documentation
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