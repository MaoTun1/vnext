# vNext Metabase Dashboards

Bu klasör, vNext workflow projesindeki her bir sistem şeması için kapsamlı Metabase dashboard'ları içermektedir.

## Dashboard'lar

### 1. Sys-Flows Dashboard (`sys-flows-dashboard.json`)
**Workflow Instance Monitoring**
- Aktif workflow sayısı ve durum dağılımı
- Günlük tamamlanan workflow'lar
- Hatalı workflow'lar ve analizi
- Ortalama süre performansı
- Domain'e göre workflow dağılımı
- Mevcut durum (CurrentState) analizi
- Tag analizi ve kullanım istatistikleri

### 2. Sys-Functions Dashboard (`sys-functions-dashboard.json`)
**Function Instance Monitoring**
- Aktif function sayısı ve yürütme metrikleri
- Function tipi bazında performans analizi
- En çok kullanılan function'lar
- Başarı oranı ve hata analizi
- Yürütme süresi scatter plot'u
- Saatlik yürütme heatmap'i
- Versiyon takibi ve konfigürasyon analizi

### 3. Sys-Schemas Dashboard (`sys-schemas-dashboard.json`)
**Schema Definition Monitoring**
- Aktif şema sayısı ve kayıt metrikleri
- Şema tipi dağılımı ve analizi
- Domain bazında şema kullanımı
- Versiyon takibi ve yaşam döngüsü
- Hatalı şema operasyonları
- İçerik boyutu ve tag analizi

### 4. Sys-Tasks Dashboard (`sys-tasks-dashboard.json`)
**Task Instance Monitoring**
- Aktif task sayısı ve yürütme metrikleri
- Task tipi dağılımı (DaprHttpEndpointTask, ScriptTask, vs.)
- En çok kullanılan task'lar
- Performance scatter plot (süre vs. zaman)
- Domain bazında başarı oranları
- Yürütme heatmap'i (saat/gün)
- Konfigürasyon analizi ve tag kullanımı

### 5. Sys-Views Dashboard (`sys-views-dashboard.json`)
**View Instance Monitoring**
- Aktif view sayısı ve oluşturma metrikleri
- View tipi ve target analizi
- Domain bazında view kullanımı
- İçerik boyutu dağılımı
- Versiyon takibi
- Hatalı view operasyonları
- Tag analizi

### 6. Sys-Extensions Dashboard (`sys-extensions-dashboard.json`)
**Extension Instance Monitoring**
- Aktif extension sayısı ve tetikleme metrikleri
- Extension tipi ve scope dağılımı
- En çok kullanılan extension'lar
- Performance scatter plot
- Domain bazında başarı oranları
- Task konfigürasyon analizi
- Yürütme heatmap'i ve tag analizi

## Dashboard Parametreleri

Her dashboard aşağıdaki parametreleri destekler:
- **Date Range**: Tarih aralığı seçimi (varsayılan: son 30 gün)
- **Domain Filter**: Belirli bir domain'e filtreleme
- **Key Filter**: Belirli bir anahtar değere filtreleme
- **Type Filter**: Belirli bir tipe filtreleme (dashboard'a göre değişir)

## Kurulum ve Kullanım

### 🚀 Hızlı Başlangıç (Docker ile)

**Tek komutla başlatma:**
```bash
cd vnext/metabase-dashboards
./metabase-setup.sh
```

**Erişim:**
- Metabase: http://localhost:3001
- PostgreSQL: localhost:5432 (postgres/postgres)

### 📋 Detaylı Kurulum

1. **Docker Compose ile Metabase Başlatma**
   ```bash
   cd vnext/etc/docker
   docker-compose up -d metabase
   ```

2. **İlk Konfigürasyon**
   - http://localhost:3001 adresine gidin
   - Admin kullanıcı oluşturun
   - Database bağlantısı ekleyin:
     ```
     Type: PostgreSQL
     Host: postgres
     Port: 5432
     Database: postgres
     Username: postgres
     Password: postgres
     ```

3. **Dashboard Import'u**
   - Dashboards → Import Dashboard
   - JSON dosyalarını sırayla import edin

### 📚 Detaylı Rehberler
- [Hızlı Başlangıç](docker-quick-start.md)
- [Detaylı Kurulum Rehberi](setup-instructions.md)
- [Dashboard Mimarisi](dashboard-overview.md)

### 3. Tablo Yapısı Gereksinimleri
Her şema için aşağıdaki tablolar gereklidir:
- `{schema}.Instances` - Ana instance tablosu
- `{schema}.InstancesData` - Instance data tablosu (JSON veriler)
- Gerekli index'ler performans için

## Önemli Notlar

### Performance Optimizasyonu
- Büyük dataset'ler için tarih aralığını sınırlayın
- Index'lerin doğru şekilde tanımlandığından emin olun
- Uzun sorguları önlemek için LIMIT kullanın

### Güvenlik
- Database kullanıcısının sadece gerekli izinlere sahip olduğundan emin olun
- Sensitive data'nın dashboard'larda görüntülenmediğini kontrol edin

### Bakım
- Dashboard'ları düzenli olarak güncelleyin
- Yeni şema alanları eklendiğinde query'leri adapte edin
- Performance metriklerini takip edin

## Katkıda Bulunma

Dashboard'larda iyileştirme yapmak için:
1. JSON dosyalarını edit edin
2. Test ortamında deneyin
3. Pull request oluşturun
4. Documentation'ı güncelleyin

## Sorun Giderme

### Yaygın Sorunlar
1. **Dashboard boş görünüyor**: Database bağlantısını ve table adlarını kontrol edin
2. **Yavaş loading**: Index'leri kontrol edin, date range'i daraltın
3. **Permission hatası**: Database user izinlerini kontrol edin

### Log Kontrolü
- Metabase log dosyalarını kontrol edin
- Database query log'larını inceleyin
- Browser console'da hataları kontrol edin

## İletişim

Sorular ve öneriler için: [Proje Repository'si]
