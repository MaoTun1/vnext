# vNext Metabase Dashboard Overview

Bu dokuman, vNext workflow projesinin sistem şemaları için oluşturulan Metabase dashboard'larının kapsamlı bir özetini sunar.

## Dashboard Mimarisi

### Genel Yaklaşım
Her sistem şeması (`sys-flows`, `sys-functions`, `sys-schemas`, `sys-tasks`, `sys-views`, `sys-extensions`) için özel olarak tasarlanmış dashboard'lar:

1. **Real-time Monitoring**: Anlık durum ve metrikler
2. **Historical Analysis**: Zaman içindeki trend analizi
3. **Performance Insights**: Performans metrikleri ve optimizasyon fırsatları
4. **Error Tracking**: Hata analizi ve sorun giderme
5. **Usage Analytics**: Kullanım desenleri ve istatistikler

## Temel Metrikler

### Her Dashboard'da Bulunan Ortak Metrikler

#### Status Metrics
- **Total Active**: Aktif instance sayısı
- **Completed Today**: Bugün tamamlanan işlemler
- **Failed Operations**: Hatalı işlemler
- **Average Duration**: Ortalama işlem süresi

#### Distribution Analysis
- **Status Distribution**: Durum dağılımı (Active, Completed, Faulted, Busy)
- **Time Series Analysis**: Zaman içindeki değişim
- **Domain/Key Distribution**: Anahtar değerlere göre dağılım

#### Performance Monitoring
- **Execution Performance**: İşlem performansı scatter plot'ları
- **Success Rates**: Başarı oranları
- **Duration Analysis**: Süre analizi
- **Heatmaps**: Yürütme desenleri

## Şema-Specific Özellikler

### 1. Sys-Flows (Workflow Management)
**Özel Fokus**: Workflow lifecycle management
- Current state tracking
- Workflow tag analysis
- Domain-based workflow distribution
- Transition success rates

### 2. Sys-Functions (Function Execution)
**Özel Fokus**: Function performance ve usage
- Function type analysis
- Execution frequency heatmaps
- Version tracking
- BFF ve calculation method monitoring

### 3. Sys-Schemas (Schema Management)
**Özel Fokus**: Schema definition lifecycle
- Schema type distribution
- Content size analysis
- Version management
- Domain schema usage patterns

### 4. Sys-Tasks (Task Execution)
**Özel Fokus**: Task performance ve monitoring
- Task type analysis (Dapr, HTTP, Script, Human)
- Configuration analysis
- Multi-type performance comparison
- Task success patterns

### 5. Sys-Views (View Management)
**Özel Fokus**: View content ve usage
- View type (JSON, etc.) analysis
- Target distribution
- Content size distribution
- View version tracking

### 6. Sys-Extensions (Extension Management)
**Özel Fokus**: Extension execution ve scope
- Extension type ve scope analysis
- Task configuration monitoring
- Execution timing patterns
- Domain-based extension usage

## Query Optimization Stratejileri

### Native SQL Usage
Gelişmiş analizler için native PostgreSQL sorguları:
- JSON field extraction
- Array operations (Tags)
- Complex aggregations
- Performance-optimized queries

### Index Requirements
```sql
-- Önerilen index'ler (her şema için)
CREATE INDEX idx_instances_flow_status_created 
ON {schema}.Instances (Flow, Status, CreatedAt);

CREATE INDEX idx_instances_key_created 
ON {schema}.Instances (Key, CreatedAt);

CREATE INDEX idx_instancesdata_instance_latest 
ON {schema}.InstancesData (InstanceId, IsLatest);

-- JSON field'ları için
CREATE INDEX idx_instancesdata_type 
ON {schema}.InstancesData USING gin ((Data ->> 'Type'));
```

## Filtering ve Parameterization

### Dashboard Parametreleri
1. **Date Range**: Flexible date range selection
2. **Domain Filter**: Multi-domain support
3. **Type Filter**: Schema-specific type filtering
4. **Key Filter**: Specific instance filtering

### Dynamic Filtering
- Template tags kullanılarak dynamic query generation
- Optional parameter support (`[[AND ...]]` syntax)
- Cross-dashboard consistency

## Performance Considerations

### Query Optimization
- Limit kullanımı büyük dataset'ler için
- Date range filtreleme performans için kritik
- Index-friendly query patterns
- Aggregation optimizations

### Dashboard Load Time
- Card count optimization
- Query complexity balance
- Auto-refresh intervals
- Cache strategy

## Monitoring Best Practices

### Real-time Monitoring
- Status dashboard'larını primary ekranlarda göster
- Critical metrics için alert'ler kur
- Performance threshold'larını belirle

### Historical Analysis
- Trend analysis için weekly/monthly görünümler
- Capacity planning için historical data
- Pattern recognition için long-term data

### Error Tracking
- Failed operations için immediate notification
- Error pattern analysis
- Root cause analysis için detailed error tables

## Customization Guidelines

### Dashboard Modifications
1. **Card Addition**: Yeni metrikler için template follow etme
2. **Query Modifications**: Performance impact assessment
3. **Visualization Changes**: User experience consistency
4. **Parameter Extensions**: Backward compatibility

### New Schema Addition
Yeni sistem şeması için dashboard oluştururken:
1. Existing pattern'ları follow et
2. Schema-specific requirements belirle  
3. Common metrics ile uyumluluğu sağla
4. Performance test'leri yap

## Security ve Access Control

### Data Privacy
- Sensitive information masking
- User-based data filtering  
- Audit log requirements

### Access Management
- Role-based dashboard access
- Schema-level permissions
- Export restrictions

## Future Enhancements

### Planned Features
1. **Real-time Alerts**: Critical threshold alerts
2. **Machine Learning**: Anomaly detection
3. **Predictive Analytics**: Capacity forecasting
4. **Cross-Schema Analytics**: Inter-schema relationship analysis

### Integration Opportunities
1. **Grafana Integration**: Infrastructure metrics ile combine
2. **Slack/Teams Notifications**: Alert integrations
3. **API Integration**: Programmatic dashboard updates
4. **CI/CD Integration**: Deployment impact monitoring

## Troubleshooting Guide

### Common Issues
1. **Empty Dashboards**: Connection ve permission kontrolü
2. **Slow Performance**: Index ve query optimization
3. **Data Inconsistency**: Schema synchronization kontrolü
4. **Visualization Errors**: Data type ve format kontrolü

### Debugging Steps
1. Database connection test
2. Query execution plan analysis
3. Index usage verification
4. Data validation checks

Bu dashboard suite, vNext workflow sisteminin comprehensive monitoring ve analytics ihtiyaçlarını karşılamak üzere tasarlanmıştır ve sürekli geliştirilecektir.

