# Getting Started

## Prerequisites

Before setting up the BBT Workflow Engine, ensure you have the following installed:

### Required Software

- **.NET 9 SDK** - [Download from Microsoft](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Docker Desktop** - [Download from Docker](https://www.docker.com/products/docker-desktop/)
- **PostgreSQL 13+** - [Download from PostgreSQL](https://www.postgresql.org/download/)
- **Redis 6+** - [Download from Redis](https://redis.io/download/)

### Optional Tools

- **DAPR CLI** - [Install guide](https://docs.dapr.io/getting-started/install-dapr-cli/)
- **PowerShell Core** - For running setup scripts
- **Visual Studio 2022** or **VS Code** - For development

## Installation

### 1. Clone the Repository

```bash
git clone <repository-url>
cd workflowV2
```

### 2. Verify .NET Version

Check that you have the correct .NET version:

```bash
dotnet --version
# Should show 9.0.x or higher
```

### 3. Restore Dependencies

```bash
dotnet restore
```

### 4. Build the Solution

```bash
dotnet build
```

## Configuration

### 1. Database Setup

#### Option A: Using Docker (Recommended for Development)

```bash
cd etc/docker

# Run both APIs in development mode
./run-docker.sh 

# Run both APIs in debugging mode (with debugger and hot reload)
./run-docker.sh dev

# Run both APIs in stage mode (no debugger)
./run-docker.sh stage
```

### 2. Configuration Files

The system now uses two separate API configurations:

#### Orchestration API Configuration
Update the connection strings in `orchestration/BBT.Workflow.Orchestration.HttpApi.Host/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=Aether_WorkflowDb;Username=postgres;Password=postgres;"
  },
  "Redis": {
    "Mode": "Standalone",
    "InstanceName": "workflow-orchestration",
    "ConnectionTimeout": 5000,
    "DefaultDatabase": 0,
    "Password": "",
    "Ssl": false,
    "Standalone": {
      "EndPoints": ["localhost:6379"]
    }
  },
  "ExecutionApi": {
    "BaseUrl": "http://localhost:4201"
  }
}
```

#### Execution API Configuration
Update the connection strings in `execution/BBT.Workflow.Execution.HttpApi.Host/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=Aether_WorkflowDb;Username=postgres;Password=postgres;"
  },
  "Redis": {
    "Mode": "Standalone",
    "InstanceName": "workflow-execution",
    "ConnectionTimeout": 5000,
    "DefaultDatabase": 0,
    "Password": "",
    "Ssl": false,
    "Standalone": {
      "EndPoints": ["localhost:6379"]
    }
  }
}
```

### 3. Environment Variables

Set the following environment variables:

```bash
# Required
export ASPNETCORE_ENVIRONMENT=Development
export APP_DOMAIN=amorphie

# Optional (for DAPR integration)
export DAPR_HTTP_PORT=42110
export DAPR_GRPC_PORT=42111

# For Execution API integration
export EXECUTION_API_BASE_URL=http://localhost:4202
```

## Running the Application

### Option 1: Using Docker (Recommended)

```bash
cd etc/docker
./run-docker.sh
```

Both APIs will start simultaneously:
- **Orchestration API**: `http://localhost:4201` (Client-facing)
- **Execution API**: `http://localhost:4202` (Internal)

### Option 2: Direct .NET Run

#### Start Both APIs Simultaneously

```bash
# Terminal 1 - Start Orchestration API
cd orchestration/BBT.Workflow.Orchestration.HttpApi.Host
dotnet run --urls="http://localhost:4201;https://localhost:7189"

# Terminal 2 - Start Execution API
cd execution/BBT.Workflow.Execution.HttpApi.Host
dotnet run --urls="http://localhost:4202;https://localhost:7190"
```

### Option 3: Using Visual Studio / VS Code

1. Open the solution file `BBT.Workflow.sln`
2. Set multiple startup projects:
   - `BBT.Workflow.Orchestration.HttpApi.Host`
   - `BBT.Workflow.Execution.HttpApi.Host`
3. Press F5 to run both APIs

## API Endpoints

### Orchestration API (Client-facing)
- HTTP: `http://localhost:4201`
- HTTPS: `https://localhost:7189`
- Swagger: `http://localhost:4201/swagger`
- Health Check: `http://localhost:4201/health`

### Execution API (Internal)
- HTTP: `http://localhost:4202`
- HTTPS: `https://localhost:7190`
- Swagger: `http://localhost:4202/swagger`
- Health Check: `http://localhost:4202/health`

## Verify Installation

### 1. Health Check

Check both APIs are running:

**Orchestration API:**
```bash
curl http://localhost:4201/health
```

**Execution API:**
```bash
curl http://localhost:4202/health
```

You should see a response like:
```json
{
  "status": "Healthy",
  "info": [
    {
      "key": "database",
      "status": "Healthy"
    },
    {
      "key": "redis",
      "status": "Healthy"
    }
  ]
}
```

### 2. API Documentation

Visit the Swagger UI for both APIs:

**Orchestration API (Client-facing):**
```
http://localhost:4201/swagger
```

**Execution API (Internal):**
```
http://localhost:4202/swagger
```

### 3. Service Communication

Test that both APIs are working properly and can communicate with each other. Here are some practical examples:

#### Basic Connectivity Test

```bash
# Test Orchestration API health
curl http://localhost:4201/health

# Test Execution API health  
curl http://localhost:4202/health
```

#### Workflow Creation and Execution Test

Based on the examples in `/examples/sub-flows/03-main-flow-with-subflow.http`, here's a practical workflow test:

```bash
# 1. Create a main flow with subflow integration
curl -X POST http://localhost:4201/api/v1/amorphie/workflows/sys-flows/instances/start?version=1.0.0 \
  -H "Content-Type: application/json" \
  -d '{
    "key": "account-opening",
    "tags": ["mainflow", "account", "subflow-test"],
    "attributes": {
      "type": "F",
      "timeout": {
        "key": "timeout",
        "target": "timeout",
        "versionStrategy": "Minor",
        "timer": {
          "reset": "never",
          "duration": "PT10M"
        }
      },
      "labels": [
        {
          "label": "Account Opening with SubFlow",
          "language": "en-US"
        }
      ],
      "startTransition": {
        "key": "start",
        "target": "initial-data",
        "triggerType": 0,
        "versionStrategy": "Minor"
      },
      "states": [
        {
          "key": "initial-data",
          "stateType": 1,
          "versionStrategy": "Minor",
          "labels": [
            {
              "label": "Initial Data Collection",
              "language": "en-US"
            }
          ],
          "transitions": [
            {
              "key": "proceed-to-validation",
              "from": "initial-data",
              "target": "customer-validation-subflow",
              "triggerType": 0,
              "versionStrategy": "Minor"
            }
          ]
        },
        {
          "key": "customer-validation-subflow",
          "stateType": 4,
          "versionStrategy": "Minor",
          "labels": [
            {
              "label": "Customer Validation (SubFlow)",
              "language": "en-US"
            }
          ],
          "subFlow": {
            "type": "S",
            "process": {
              "key": "customer-validation",
              "domain": "amorphie", 
              "flow": "sys-flows",
              "version": "1.0.0"
            }
          },
          "transitions": [
            {
              "key": "subflow-completed",
              "from": "customer-validation-subflow",
              "target": "document-processing",
              "triggerType": 1,
              "versionStrategy": "Minor"
            }
          ]
        }
      ]
    }
  }'

# 2. Start a workflow instance
curl -X POST http://localhost:4201/api/v1/amorphie/workflows/account-opening/instances/start?version=1.0.0 \
  -H "Content-Type: application/json" \
  -d '{
    "key": "12345678901",
    "tags": ["test", "mainflow", "subflow-integration"],
    "attributes": {
      "applicantId": "MAIN-001",
      "accountType": "checking",
      "customerData": {
        "customerId": "12345",
        "customerType": "individual",
        "validationData": {
          "identityNumber": "12345678901",
          "phoneNumber": "+905551234567",
          "email": "test@example.com"
        }
      }
    }
  }'

# 3. Get instance details (replace {instanceId} with actual ID from step 2 response)
curl http://localhost:4201/api/v1/amorphie/workflows/account-opening/instances/{instanceId}

# 4. Execute a transition (this will trigger communication between Orchestration and Execution APIs)
curl -X PATCH http://localhost:4201/api/v1/amorphie/workflows/account-opening/instances/{instanceId}/transitions/proceed-to-validation?version=1.0.0 \
  -H "Content-Type: application/json" \
  -d '{
    "dataCollected": true,
    "collectedAt": "2024-01-15T10:00:00Z"
  }'
```

#### Monitoring API Communication

During workflow execution, you can monitor the communication between APIs:

```bash
# Monitor Orchestration API logs
tail -f orchestration/BBT.Workflow.Orchestration.HttpApi.Host/Logs/workflow-orchestration.log

# Monitor Execution API logs (in another terminal)
tail -f execution/BBT.Workflow.Execution.HttpApi.Host/Logs/workflow-execution.log
```

When you execute transitions, you should see:
1. **Orchestration API** receives the client request
2. **Orchestration API** processes workflow logic
3. **Orchestration API** calls **Execution API** for task execution
4. **Execution API** processes and executes tasks
5. Both APIs coordinate to complete the workflow transition

#### Expected Behavior

- **Successful Communication**: Workflow transitions execute smoothly
- **Failed Communication**: You'll see connection errors in logs if Execution API is not reachable
- **Task Execution**: Complex tasks (SubFlows, HTTP calls, etc.) are handled by the Execution API

## Architecture Overview

With the new microservices architecture:

1. **Orchestration API** handles all client interactions
2. **Execution API** processes tasks internally
3. The Orchestration API communicates with the Execution API when task execution is needed
4. Both APIs share the same domain and infrastructure layers

```
[Client] → [Orchestration API] → [Execution API]
           ↓
       [Shared Domain & Infrastructure]
```

## Development Tips

### 1. Debugging Multiple APIs

When debugging, you can:
- Use different IDE instances for each API
- Use Docker Compose for easier management
- Check logs from both services

### 2. API Communication

- The Orchestration API should be your primary entry point
- The Execution API is for internal use only
- Monitor both APIs' health endpoints

### 3. Configuration Management

- Keep both APIs' configurations in sync for shared resources (database, Redis)
- Use different instance names for Redis to avoid conflicts
- Environment-specific configurations are maintained separately

## Development Tools

### Workflow Development Automation

For efficient workflow development, use the automated CSX to JSON conversion tools:

```bash
# Install Node.js if not already installed
node --version

# Update single workflow (when editing CSX files)
node .vscode/scripts/update-workflow-rules.js examples/my-workflow

# Update all workflows
node .vscode/scripts/update-all-workflows.js

# Start file watcher for automatic updates
node .vscode/scripts/watch-workflows.js
```

**VS Code Integration:**
- Use `Ctrl + Shift + W` to update current workflow
- Use `Ctrl + Shift + Alt + W` to update all workflows
- File watcher automatically updates JSON when CSX files change

## Next Steps

1. **Explore the API Documentation**: Check both Swagger UIs to understand available endpoints
2. **Set Up Development Automation**: Configure VS Code tools for efficient workflow development
3. **Create More Complex Workflows**: Add conditional logic, parallel tasks, and subflows
4. **Implement Custom Tasks**: Create your own task executors
5. **Set Up Monitoring**: Configure logging and monitoring for both APIs
6. **Deploy to Production**: Use the provided Docker configurations for deployment

## Troubleshooting

### Common Issues

1. **APIs won't start**: Check if ports 4201 and 4202 are available
2. **Database connection errors**: Verify PostgreSQL is running and connection strings are correct
3. **Redis connection errors**: Ensure Redis is running and accessible
4. **Internal API communication errors**: Verify the Execution API base URL configuration

### Log Locations

- **Orchestration API logs**: `orchestration/BBT.Workflow.Orchestration.HttpApi.Host/Logs/`
- **Execution API logs**: `execution/BBT.Workflow.Execution.HttpApi.Host/Logs/`

For more detailed information, refer to the [Architecture Overview](architecture-overview.md) and other documentation files. 