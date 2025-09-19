#!/bin/bash

echo "🎯 vNext Key-Based Instance Dashboard Creator"
echo "=============================================="

# Configuration
METABASE_URL="http://localhost:3001"
DATABASE_NAME="Aether_WorkflowDb"

# Get credentials
echo "🔐 Metabase login bilgileri:"
read -p "Metabase Email: " EMAIL
read -s -p "Metabase Password: " PASSWORD
echo ""

# Login to Metabase
echo "🔑 Metabase'e giriş yapılıyor..."
LOGIN_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -d "{\"username\":\"$EMAIL\",\"password\":\"$PASSWORD\"}" \
    "$METABASE_URL/api/session")

SESSION_TOKEN=$(echo $LOGIN_RESPONSE | grep -o '"id":"[^"]*"' | cut -d'"' -f4)

if [ -z "$SESSION_TOKEN" ]; then
    echo "❌ Login başarısız: $LOGIN_RESPONSE"
    exit 1
fi

echo "✅ Login başarılı!"

# Check if database exists
echo "🔍 Database bağlantıları kontrol ediliyor..."
DATABASES_RESPONSE=$(curl -s -X GET \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    "$METABASE_URL/api/database")

DATABASE_ID=$(echo $DATABASES_RESPONSE | grep -o '"name":"'$DATABASE_NAME'"[^}]*"id":[0-9]*' | grep -o '"id":[0-9]*' | cut -d':' -f2)

if [ -z "$DATABASE_ID" ]; then
    echo "🆕 Aether_WorkflowDb database bağlantısı oluşturuluyor..."
    
    DB_CREATE_RESPONSE=$(curl -s -X POST \
        -H "Content-Type: application/json" \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        -d '{
            "engine": "postgres",
            "name": "Aether_WorkflowDb",
            "details": {
                "host": "postgres",
                "port": 5432,
                "dbname": "Aether_WorkflowDb",
                "user": "postgres",
                "password": "postgres",
                "ssl": false,
                "additional-options": "",
                "tunnel-enabled": false
            }
        }' \
        "$METABASE_URL/api/database")
    
    DATABASE_ID=$(echo $DB_CREATE_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)
    
    if [ -n "$DATABASE_ID" ]; then
        echo "✅ Database bağlantısı oluşturuldu: ID $DATABASE_ID"
        
        # Sync database
        echo "🔄 Database sync ediliyor..."
        curl -s -X POST \
            -H "X-Metabase-Session: $SESSION_TOKEN" \
            "$METABASE_URL/api/database/$DATABASE_ID/sync_schema" > /dev/null
        
        sleep 5
    else
        echo "❌ Database bağlantısı oluşturulamadı: $DB_CREATE_RESPONSE"
        exit 1
    fi
else
    echo "✅ Database bağlantısı mevcut: ID $DATABASE_ID"
fi

# Test database connection
echo "🧪 Database bağlantısı test ediliyor..."
TEST_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d "{
        \"type\": \"native\",
        \"native\": {
            \"query\": \"SELECT COUNT(*) as total FROM sys_flows.\\\"Instances\\\" LIMIT 1;\"
        },
        \"database\": $DATABASE_ID
    }" \
    "$METABASE_URL/api/dataset")

if echo "$TEST_RESPONSE" | grep -q '"data"'; then
    echo "✅ Database bağlantısı çalışıyor"
else
    echo "❌ Database bağlantısı hatası: $TEST_RESPONSE"
    exit 1
fi

# Create dashboard
echo "📋 Dashboard oluşturuluyor..."
DASHBOARD_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d '{
        "name": "vNext Instance Key-Based Analysis",
        "description": "Key değerine göre farklı şemalardan Instance analizi"
    }' \
    "$METABASE_URL/api/dashboard")

DASHBOARD_ID=$(echo $DASHBOARD_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)

if [ -z "$DASHBOARD_ID" ]; then
    echo "❌ Dashboard oluşturulamadı: $DASHBOARD_RESPONSE"
    exit 1
fi

echo "✅ Dashboard oluşturuldu: ID $DASHBOARD_ID"

# Add filter parameter to dashboard
echo "🔧 Dashboard parametresi ekleniyor..."
curl -s -X PUT \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d '{
        "parameters": [{
            "id": "key_filter",
            "name": "Key Filter",
            "slug": "key",
            "type": "category",
            "sectionId": "string"
        }]
    }' \
    "$METABASE_URL/api/dashboard/$DASHBOARD_ID" > /dev/null

# Function to create question and add to dashboard
create_question() {
    local name="$1"
    local query="$2"
    local display="$3"
    local row="$4"
    local col="$5"
    local size_x="$6"
    local size_y="$7"
    
    echo "📊 Soru oluşturuluyor: $name"
    
    # Escape quotes in query for JSON
    ESCAPED_QUERY=$(echo "$query" | sed 's/"/\\"/g')
    
    QUESTION_RESPONSE=$(curl -s -X POST \
        -H "Content-Type: application/json" \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        -d "{
            \"name\": \"$name\",
            \"description\": \"$name\",
            \"dataset_query\": {
                \"type\": \"native\",
                \"native\": {
                    \"query\": \"$ESCAPED_QUERY\",
                    \"template-tags\": {
                        \"key\": {
                            \"id\": \"key-filter\",
                            \"name\": \"key\",
                            \"display-name\": \"Key\",
                            \"type\": \"text\",
                            \"default\": null,
                            \"required\": false
                        }
                    }
                },
                \"database\": $DATABASE_ID
            },
            \"display\": \"$display\",
            \"visualization_settings\": {}
        }" \
        "$METABASE_URL/api/card")
    
    QUESTION_ID=$(echo $QUESTION_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)
    
    if [ -n "$QUESTION_ID" ]; then
        echo "✅ Soru oluşturuldu: $name (ID: $QUESTION_ID)"
        
        # Add to dashboard with parameter mapping
        curl -s -X PUT \
            -H "Content-Type: application/json" \
            -H "X-Metabase-Session: $SESSION_TOKEN" \
            -d "{
                \"cards\": [{
                    \"id\": -1,
                    \"card_id\": $QUESTION_ID,
                    \"row\": $row,
                    \"col\": $col,
                    \"size_x\": $size_x,
                    \"size_y\": $size_y,
                    \"series\": [],
                    \"visualization_settings\": {},
                    \"parameter_mappings\": [{
                        \"parameter_id\": \"key_filter\",
                        \"card_id\": $QUESTION_ID,
                        \"target\": [\"variable\", [\"template-tag\", \"key\"]]
                    }]
                }]
            }" \
            "$METABASE_URL/api/dashboard/$DASHBOARD_ID" > /dev/null
        
        echo "✅ Dashboard'a eklendi"
    else
        echo "❌ Soru oluşturulamadı: $QUESTION_RESPONSE"
    fi
}

echo ""
echo "📊 Dashboard sorularını oluşturuyor..."

# 1. sys_flows schema instances
create_question "sys_flows Instances" \
"SELECT 
    \"Key\",
    \"Flow\", 
    \"Status\",
    \"CompletedAt\",
    \"ModifiedAt\"
FROM sys_flows.\"Instances\"
WHERE 1=1
    [[AND \"Key\" ILIKE CONCAT('%', {{key}}, '%')]]
ORDER BY \"ModifiedAt\" DESC
LIMIT 100;" \
"table" 0 0 12 8

# 2. sys_tasks schema instances (when key contains 'sys-task')
create_question "sys_tasks Instances" \
"SELECT 
    \"Key\",
    \"Flow\", 
    \"Status\",
    \"CompletedAt\",
    \"ModifiedAt\"
FROM sys_tasks.\"Instances\"
WHERE 1=1
    [[AND \"Key\" ILIKE CONCAT('%', {{key}}, '%')]]
ORDER BY \"ModifiedAt\" DESC
LIMIT 100;" \
"table" 8 0 12 8

# 3. sys_functions schema instances
create_question "sys_functions Instances" \
"SELECT 
    \"Key\",
    \"Flow\", 
    \"Status\",
    \"CompletedAt\",
    \"ModifiedAt\"
FROM sys_functions.\"Instances\"
WHERE 1=1
    [[AND \"Key\" ILIKE CONCAT('%', {{key}}, '%')]]
ORDER BY \"ModifiedAt\" DESC
LIMIT 100;" \
"table" 16 0 12 8

# 4. sys_schemas schema instances
create_question "sys_schemas Instances" \
"SELECT 
    \"Key\",
    \"Flow\", 
    \"Status\",
    \"CompletedAt\",
    \"ModifiedAt\"
FROM sys_schemas.\"Instances\"
WHERE 1=1
    [[AND \"Key\" ILIKE CONCAT('%', {{key}}, '%')]]
ORDER BY \"ModifiedAt\" DESC
LIMIT 100;" \
"table" 24 0 12 8

# 5. Key Summary
create_question "Key Summary" \
"SELECT 
    'sys_flows' as schema_name,
    COUNT(*) as instance_count,
    COUNT(CASE WHEN \"Status\" = 'Active' THEN 1 END) as active_count,
    COUNT(CASE WHEN \"Status\" = 'Completed' THEN 1 END) as completed_count
FROM sys_flows.\"Instances\"
WHERE 1=1
    [[AND \"Key\" ILIKE CONCAT('%', {{key}}, '%')]]
UNION ALL
SELECT 
    'sys_tasks' as schema_name,
    COUNT(*) as instance_count,
    COUNT(CASE WHEN \"Status\" = 'Active' THEN 1 END) as active_count,
    COUNT(CASE WHEN \"Status\" = 'Completed' THEN 1 END) as completed_count
FROM sys_tasks.\"Instances\"
WHERE 1=1
    [[AND \"Key\" ILIKE CONCAT('%', {{key}}, '%')]]
UNION ALL
SELECT 
    'sys_functions' as schema_name,
    COUNT(*) as instance_count,
    COUNT(CASE WHEN \"Status\" = 'Active' THEN 1 END) as active_count,
    COUNT(CASE WHEN \"Status\" = 'Completed' THEN 1 END) as completed_count
FROM sys_functions.\"Instances\"
WHERE 1=1
    [[AND \"Key\" ILIKE CONCAT('%', {{key}}, '%')]]
UNION ALL
SELECT 
    'sys_schemas' as schema_name,
    COUNT(*) as instance_count,
    COUNT(CASE WHEN \"Status\" = 'Active' THEN 1 END) as active_count,
    COUNT(CASE WHEN \"Status\" = 'Completed' THEN 1 END) as completed_count
FROM sys_schemas.\"Instances\"
WHERE 1=1
    [[AND \"Key\" ILIKE CONCAT('%', {{key}}, '%')]]
ORDER BY instance_count DESC;" \
"table" 0 12 12 6

echo ""
echo "🎉 Key-Based Dashboard oluşturuldu!"
echo "🔗 Dashboard URL: $METABASE_URL/dashboard/$DASHBOARD_ID"
echo ""
echo "📊 Dashboard özellikleri:"
echo "  ✅ Key filtresi ile arama yapabilme"
echo "  ✅ sys_flows schema instance'ları"
echo "  ✅ sys_tasks schema instance'ları" 
echo "  ✅ sys_functions schema instance'ları"
echo "  ✅ sys_schemas schema instance'ları"
echo "  ✅ Key özet analizi"
echo ""
echo "🔍 Kullanım:"
echo "  1. Dashboard'a git: $METABASE_URL/dashboard/$DASHBOARD_ID"
echo "  2. 'Key Filter' alanına aradığınız key değerini girin"
echo "  3. Tüm şemalardaki matching instance'lar görüntülenecek"
echo ""
echo "🚀 Dashboard kullanıma hazır!"
