# Workflow Metrics - Grafana Dashboard

Bu klasör Workflow System için comprehensive monitoring setup'ı içerir.

## 🚀 Quick Start

### 1. Development Environment Başlatma
```bash
cd vnext/etc/docker
docker compose -f docker-compose.dev.yml up -d prometheus grafana
```

### 2. Grafana Dashboard Erişimi
- **URL**: http://localhost:3000
- **Username**: `admin`
- **Password**: `admin`

### 3. Prometheus Erişimi
- **URL**: http://localhost:9090

## 📊 Dashboard Özellikleri

### System Health Overview
- Overall System Health Status (Healthy/Unhealthy)
- Overall Error Rate (%)
- Real-time Error Rate by Type/Severity

### Workflow State Metrics
- State Transitions (per minute)
- Instance Status Distribution (pie chart)
- State Duration P95 (seconds)

### Database Metrics
- Database Queries by Type/Table (per minute)
- Query Duration P95/P50

### HTTP API Metrics
- HTTP Requests by Endpoint/Status (per minute)
- Request Duration P95
- HTTP Errors by Type

### Background Jobs & Script Engine
- Background Jobs Status (Pending/Running)
- Script Executions by Type/Language

### Cache & External Services
- Cache Hit/Miss Rates
- External Service Calls by Status
- DAPR Integration Metrics

## 🔧 Configuration Files

### Prometheus Configuration
- `config/prometheus/prometheus.yml` - Prometheus scraping configuration

### Grafana Configuration
- `config/grafana/provisioning/datasources/` - Auto-configured Prometheus datasource
- `config/grafana/provisioning/dashboards/` - Dashboard provisioning
- `config/grafana/dashboards/workflow-metrics.json` - Main workflow dashboard

## 📈 Metrics Endpoint

Workflow aplikasyonlarınız şu endpoint'ten metrics sağlıyor:
- **Orchestration API**: http://vnext-app:5000/metrics
- **Execution API**: http://vnext-execution-app:5000/metrics

## 🎯 Available Metrics

### Counter Metrics
- `workflow_state_transitions_total` - State transitions
- `workflow_errors_total` - Total errors by type/severity  
- `workflow_exceptions_total` - Unhandled exceptions
- `workflow_validation_failures_total` - Validation failures
- `http_requests_total` - HTTP requests
- `workflow_db_queries_total` - Database queries
- `script_executions_total` - Script executions
- `background_jobs_scheduled_total` - Background jobs
- `external_service_calls_total` - External service calls
- `dapr_service_invocations_total` - DAPR invocations

### Gauge Metrics
- `workflow_health_status` - System health (0=unhealthy, 1=healthy)
- `workflow_error_rate` - Current error rate %
- `workflow_instances_by_status` - Instance count by status
- `task_factory_pool_size` - Object pool metrics
- `workflow_cache_size_bytes` - Cache size
- `background_jobs_pending` - Pending job count

### Histogram Metrics
- `workflow_state_duration_seconds` - Time in each state
- `workflow_db_query_duration_seconds` - Database query time
- `http_request_duration_seconds` - HTTP request time
- `background_job_duration_seconds` - Job execution time
- `script_execution_duration_seconds` - Script execution time
- `external_service_duration_seconds` - External call time

## 🛠 Troubleshooting

### Grafana Dashboard Görünmüyor?
1. Container'ların çalışıp çalışmadığını kontrol edin:
   ```bash
   docker ps | grep -E "(grafana|prometheus)"
   ```

2. Prometheus targets'ı kontrol edin:
   - http://localhost:9090/targets

### Metrics Gelmiyor?
1. Workflow uygulamanızın `/metrics` endpoint'ini kontrol edin
2. Prometheus configuration'ında target'lar doğru mu?
3. Network connectivity kontrol edin

## 📝 Customization

Dashboard'u customize etmek için:
1. Grafana UI'dan edit yapın
2. Export edin JSON formatında
3. `config/grafana/dashboards/workflow-metrics.json` dosyasını güncelleyin