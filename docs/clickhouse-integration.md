# ClickHouse Integration

This document describes the ClickHouse integration for the BBT Workflow system, which provides real-time analytics and data warehousing capabilities for workflow instances, transitions, and tasks.

## Overview

The ClickHouse integration is implemented as a pluggable component that automatically transfers data from the main PostgreSQL database to ClickHouse for analytics purposes. This allows for:

- Real-time analytics on workflow performance
- Historical data analysis
- Performance monitoring and reporting
- Data warehousing for business intelligence

## Architecture

The integration consists of several components:

1. **ClickHouse Data Transfer Service** - Handles the actual data transfer
2. **Repository Integration** - Automatically triggers data transfer on CRUD operations
3. **Configuration Management** - Manages ClickHouse connection and settings
4. **Docker Integration** - Provides ClickHouse container setup

## Configuration

### AppSettings Configuration

The ClickHouse integration is configured through the `ClickHouse` section in appsettings.json:

```json
{
  "ClickHouse": {
    "Enabled": true,
    "ConnectionString": "Host=localhost;Port=8123;Database=workflow_analytics;Username=default;Password=;",
    "BatchSize": 1000,
    "FlushIntervalSeconds": 5,
    "RetryAttempts": 3,
    "RetryDelayMilliseconds": 1000,
    "Tables": {
      "Instances": "instances",
      "InstanceTransitions": "instance_transitions",
      "InstanceTasks": "instance_tasks"
    }
  }
}
```

### Configuration Parameters

- **Enabled**: Whether ClickHouse integration is active
- **ConnectionString**: ClickHouse connection string
- **BatchSize**: Number of records to batch before sending to ClickHouse
- **FlushIntervalSeconds**: How often to flush pending data (in seconds)
- **RetryAttempts**: Number of retry attempts for failed operations
- **RetryDelayMilliseconds**: Delay between retry attempts
- **Tables**: Mapping of entity types to ClickHouse table names

## Data Transfer Operations

The integration automatically transfers data for the following operations:

### Instance Operations
- **Insert**: When a new workflow instance is created
- **Update**: When an instance status or properties change

### Instance Transition Operations
- **Insert**: When a new transition is created
- **Update**: When a transition is completed or updated

### Instance Task Operations
- **Insert**: When a new task is created
- **Update**: When a task status or properties change

## ClickHouse Schema

### Instances Table
```sql
CREATE TABLE instances (
    Id UUID,
    Key Nullable(String),
    Flow String,
    CurrentState Nullable(String),
    Status String,
    CreatedAt DateTime64(3),
    ModifiedAt Nullable(DateTime64(3)),
    CompletedAt Nullable(DateTime64(3)),
    DurationSeconds Nullable(Float64),
    Tags String DEFAULT '[]',
    IsTransient UInt8 DEFAULT 0,
    Operation String,
    TransferTimestamp DateTime64(3) DEFAULT now64(3)
) ENGINE = MergeTree()
ORDER BY (CreatedAt, Id)
PARTITION BY toYYYYMM(CreatedAt);
```

### Instance Transitions Table
```sql
CREATE TABLE instance_transitions (
    Id UUID,
    InstanceId UUID,
    TransitionId String,
    FromState String,
    ToState Nullable(String),
    StartedAt DateTime64(3),
    FinishedAt Nullable(DateTime64(3)),
    DurationSeconds Nullable(Float64),
    Body String DEFAULT '{}',
    Header String DEFAULT '{}',
    Operation String,
    TransferTimestamp DateTime64(3) DEFAULT now64(3)
) ENGINE = MergeTree()
ORDER BY (StartedAt, Id)
PARTITION BY toYYYYMM(StartedAt);
```

### Instance Tasks Table
```sql
CREATE TABLE instance_tasks (
    Id UUID,
    TransitionId UUID,
    TaskId String,
    Status String,
    StartedAt DateTime64(3),
    FinishedAt Nullable(DateTime64(3)),
    DurationSeconds Nullable(Float64),
    FaultedTaskId Nullable(UUID),
    Request String DEFAULT '{}',
    Response String DEFAULT '{}',
    Operation String,
    TransferTimestamp DateTime64(3) DEFAULT now64(3)
) ENGINE = MergeTree()
ORDER BY (StartedAt, Id)
PARTITION BY toYYYYMM(StartedAt);
```

## Materialized Views

The integration includes several materialized views for common analytics queries:

### Instance Status Summary
Provides aggregated statistics by flow, status, and date.

### Transition Performance
Tracks transition performance metrics by transition ID and date.

### Task Performance
Monitors task performance by task type and date.

## Docker Setup

ClickHouse is included in both development and production Docker Compose configurations:

### Development
```yaml
clickhouse:
  container_name: vnext-clickhouse
  image: clickhouse/clickhouse-server:latest
  environment:
    CLICKHOUSE_DB: workflow_analytics
    CLICKHOUSE_USER: default
    CLICKHOUSE_PASSWORD: ""
  ports:
    - "8123:8123"  # HTTP interface
    - "9000:9000"  # Native interface
  volumes:
    - clickhouse:/var/lib/clickhouse
    - ./config/clickhouse:/etc/clickhouse-server/config.d
    - ./config/clickhouse/users.xml:/etc/clickhouse-server/users.xml
    - ./config/clickhouse/init.sql:/docker-entrypoint-initdb.d/init.sql
```

## Usage

### Starting the System

1. Start the Docker containers:
   ```bash
   docker-compose -f etc/docker/docker-compose.dev.yml up -d
   ```

2. The ClickHouse database and tables will be automatically created from the init.sql script.

3. The workflow system will automatically start transferring data to ClickHouse.

### Querying Data

You can query ClickHouse data using the HTTP interface:

```bash
# Get instance count by status
curl "http://localhost:8123/?query=SELECT Status, count() FROM workflow_analytics.instances GROUP BY Status"

# Get average duration by flow
curl "http://localhost:8123/?query=SELECT Flow, avg(DurationSeconds) FROM workflow_analytics.instances WHERE DurationSeconds IS NOT NULL GROUP BY Flow"
```

### Web Interface

ClickHouse provides a web interface at `http://localhost:8123/play` for interactive queries.

## Performance Considerations

### Batching
- Data is batched before sending to ClickHouse to improve performance
- Default batch size is 1000 records
- Batches are automatically flushed based on time intervals

### Error Handling
- Failed transfers are retried with exponential backoff
- Errors are logged but don't affect the main workflow operations
- The system continues to function even if ClickHouse is unavailable

### Monitoring
- Transfer operations are logged for monitoring
- Failed transfers are tracked and can be monitored
- Performance metrics are available through the standard logging system

## Troubleshooting

### Common Issues

1. **ClickHouse Connection Failed**
   - Check if ClickHouse container is running
   - Verify connection string in appsettings.json
   - Check network connectivity between containers

2. **Data Not Appearing in ClickHouse**
   - Verify ClickHouse integration is enabled in configuration
   - Check application logs for transfer errors
   - Ensure tables are created properly

3. **Performance Issues**
   - Adjust batch size and flush interval
   - Monitor ClickHouse resource usage
   - Consider ClickHouse cluster setup for high-volume scenarios

### Logs

Check application logs for ClickHouse-related messages:
```bash
docker logs vnext-app | grep -i clickhouse
```

## Future Enhancements

Potential future improvements include:

1. **Real-time Dashboards** - Integration with Grafana for real-time monitoring
2. **Data Retention Policies** - Automatic cleanup of old data
3. **Advanced Analytics** - Machine learning integration for predictive analytics
4. **Multi-tenant Support** - Schema-based data isolation
5. **Data Compression** - Advanced compression strategies for cost optimization

