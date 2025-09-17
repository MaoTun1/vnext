#!/bin/bash

echo "🚀 Comprehensive vNext System Dashboard Creator"
echo "=============================================="

# Login to Metabase
echo "🔐 Logging in to Metabase..."

read -p "Metabase Email: " EMAIL
read -p "Metabase Password: " PASSWORD

LOGIN_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -d "{\"username\":\"$EMAIL\",\"password\":\"$PASSWORD\"}" \
    "http://localhost:3001/api/session")

SESSION_TOKEN=$(echo $LOGIN_RESPONSE | grep -o '"id":"[^"]*"' | cut -d'"' -f4)

if [ -z "$SESSION_TOKEN" ]; then
    echo "❌ Login failed. Response: $LOGIN_RESPONSE"
    exit 1
fi

echo "✅ Login successful!"

# Create comprehensive dashboard
echo "📋 Creating comprehensive dashboard..."

DASHBOARD_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d '{
        "name": "vNext System Comprehensive Analysis",
        "description": "Complete analysis of all schemas, instances, data, and transitions"
    }' \
    "http://localhost:3001/api/dashboard")

DASHBOARD_ID=$(echo $DASHBOARD_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)

if [ -z "$DASHBOARD_ID" ]; then
    echo "❌ Failed to create dashboard: $DASHBOARD_RESPONSE"
    exit 1
fi

echo "✅ Dashboard created with ID: $DASHBOARD_ID"

# Function to create question and add to dashboard
create_question() {
    local name="$1"
    local query="$2"
    local display="$3"
    local row="$4"
    local col="$5"
    local size_x="$6"
    local size_y="$7"
    
    echo "📊 Creating question: $name"
    
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
                    \"query\": \"$ESCAPED_QUERY\"
                },
                \"database\": 2
            },
            \"display\": \"$display\",
            \"visualization_settings\": {}
        }" \
        "http://localhost:3001/api/card")
    
    QUESTION_ID=$(echo $QUESTION_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)
    
    if [ -n "$QUESTION_ID" ]; then
        echo "✅ Question created: $name (ID: $QUESTION_ID)"
        
        # Add to dashboard
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
                    \"parameter_mappings\": []
                }]
            }" \
            "http://localhost:3001/api/dashboard/$DASHBOARD_ID" > /dev/null
        
        echo "✅ Added to dashboard"
    else
        echo "❌ Failed to create question: $QUESTION_RESPONSE"
    fi
}

echo ""
echo "📊 Creating comprehensive analysis questions..."

# 1. Schema Overview - Instance Counts by Schema
create_question "Schema Overview - Instance Counts" \
"SELECT 
    CASE 
        WHEN table_schema = 'sys_flows' THEN 'sys-flows'
        WHEN table_schema = 'sys_functions' THEN 'sys-functions' 
        WHEN table_schema = 'sys_schemas' THEN 'sys-schemas'
        WHEN table_schema = 'sys_tasks' THEN 'sys-tasks'
        WHEN table_schema = 'sys_views' THEN 'sys-views'
        WHEN table_schema = 'sys_extensions' THEN 'sys-extensions'
        WHEN table_schema = 'checking_account_opening' THEN 'checking_account_opening'
        ELSE table_schema
    END as \"Schema\",
    COUNT(*) as \"Instance Count\"
FROM information_schema.tables t
LEFT JOIN (
    SELECT 'sys_flows' as schema_name, COUNT(*) as cnt FROM sys_flows.\"Instances\"
    UNION ALL
    SELECT 'sys_functions' as schema_name, COUNT(*) as cnt FROM sys_functions.\"Instances\"
    UNION ALL  
    SELECT 'sys_schemas' as schema_name, COUNT(*) as cnt FROM sys_schemas.\"Instances\"
    UNION ALL
    SELECT 'sys_tasks' as schema_name, COUNT(*) as cnt FROM sys_tasks.\"Instances\"
    UNION ALL
    SELECT 'sys_views' as schema_name, COUNT(*) as cnt FROM sys_views.\"Instances\"
    UNION ALL
    SELECT 'sys_extensions' as schema_name, COUNT(*) as cnt FROM sys_extensions.\"Instances\"
    UNION ALL
    SELECT 'checking_account_opening' as schema_name, COUNT(*) as cnt FROM checking_account_opening.\"Instances\"
) counts ON t.table_schema = counts.schema_name
WHERE t.table_name = 'Instances' 
    AND t.table_schema IN ('sys_flows', 'sys_functions', 'sys_schemas', 'sys_tasks', 'sys_views', 'sys_extensions', 'checking_account_opening')
GROUP BY t.table_schema, counts.cnt
ORDER BY counts.cnt DESC;" \
"table" 0 0 12 6

# 2. Status Distribution - Active vs Completed
create_question "Status Distribution - Active vs Completed" \
"SELECT 
    'sys-flows' as \"Schema\",
    \"Status\",
    COUNT(*) as \"Count\"
FROM sys_flows.\"Instances\"
GROUP BY \"Status\"
UNION ALL
SELECT 
    'sys-functions' as \"Schema\",
    \"Status\",
    COUNT(*) as \"Count\"
FROM sys_functions.\"Instances\"
GROUP BY \"Status\"
UNION ALL
SELECT 
    'sys-schemas' as \"Schema\",
    \"Status\",
    COUNT(*) as \"Count\"
FROM sys_schemas.\"Instances\"
GROUP BY \"Status\"
UNION ALL
SELECT 
    'sys-tasks' as \"Schema\",
    \"Status\",
    COUNT(*) as \"Count\"
FROM sys_tasks.\"Instances\"
GROUP BY \"Status\"
UNION ALL
SELECT 
    'sys-views' as \"Schema\",
    \"Status\",
    COUNT(*) as \"Count\"
FROM sys_views.\"Instances\"
GROUP BY \"Status\"
UNION ALL
SELECT 
    'sys-extensions' as \"Schema\",
    \"Status\",
    COUNT(*) as \"Count\"
FROM sys_extensions.\"Instances\"
GROUP BY \"Status\"
UNION ALL
SELECT 
    'checking_account_opening' as \"Schema\",
    \"Status\",
    COUNT(*) as \"Count\"
FROM checking_account_opening.\"Instances\"
GROUP BY \"Status\"
ORDER BY \"Schema\", \"Status\";" \
"table" 0 12 12 6

# 3. InstanceData Count per Instance
create_question "InstanceData Count per Instance" \
"SELECT 
    'sys-flows' as \"Schema\",
    AVG(data_count) as \"Avg Data Count\",
    MAX(data_count) as \"Max Data Count\",
    MIN(data_count) as \"Min Data Count\"
FROM (
    SELECT i.\"Id\", COUNT(id.\"Id\") as data_count
    FROM sys_flows.\"Instances\" i
    LEFT JOIN sys_flows.\"InstancesData\" id ON i.\"Id\" = id.\"InstanceId\"
    GROUP BY i.\"Id\"
) counts
UNION ALL
SELECT 
    'sys-functions' as \"Schema\",
    AVG(data_count) as \"Avg Data Count\",
    MAX(data_count) as \"Max Data Count\",
    MIN(data_count) as \"Min Data Count\"
FROM (
    SELECT i.\"Id\", COUNT(id.\"Id\") as data_count
    FROM sys_functions.\"Instances\" i
    LEFT JOIN sys_functions.\"InstancesData\" id ON i.\"Id\" = id.\"InstanceId\"
    GROUP BY i.\"Id\"
) counts
UNION ALL
SELECT 
    'sys-tasks' as \"Schema\",
    AVG(data_count) as \"Avg Data Count\",
    MAX(data_count) as \"Max Data Count\",
    MIN(data_count) as \"Min Data Count\"
FROM (
    SELECT i.\"Id\", COUNT(id.\"Id\") as data_count
    FROM sys_tasks.\"Instances\" i
    LEFT JOIN sys_tasks.\"InstancesData\" id ON i.\"Id\" = id.\"InstanceId\"
    GROUP BY i.\"Id\"
) counts
ORDER BY \"Schema\";" \
"table" 6 0 12 6

# 4. InstanceTransition Count per Instance  
create_question "InstanceTransition Count per Instance" \
"SELECT 
    'sys-flows' as \"Schema\",
    AVG(transition_count) as \"Avg Transitions\",
    MAX(transition_count) as \"Max Transitions\",
    MIN(transition_count) as \"Min Transitions\"
FROM (
    SELECT i.\"Id\", COUNT(it.\"Id\") as transition_count
    FROM sys_flows.\"Instances\" i
    LEFT JOIN sys_flows.\"InstanceTransitions\" it ON i.\"Id\" = it.\"InstanceId\"
    GROUP BY i.\"Id\"
) counts
UNION ALL
SELECT 
    'sys-functions' as \"Schema\",
    AVG(transition_count) as \"Avg Transitions\",
    MAX(transition_count) as \"Max Transitions\",
    MIN(transition_count) as \"Min Transitions\"
FROM (
    SELECT i.\"Id\", COUNT(it.\"Id\") as transition_count
    FROM sys_functions.\"Instances\" i
    LEFT JOIN sys_functions.\"InstanceTransitions\" it ON i.\"Id\" = it.\"InstanceId\"
    GROUP BY i.\"Id\"
) counts
UNION ALL
SELECT 
    'sys-tasks' as \"Schema\",
    AVG(transition_count) as \"Avg Transitions\",
    MAX(transition_count) as \"Max Transitions\",
    MIN(transition_count) as \"Min Transitions\"
FROM (
    SELECT i.\"Id\", COUNT(it.\"Id\") as transition_count
    FROM sys_tasks.\"Instances\" i
    LEFT JOIN sys_tasks.\"InstanceTransitions\" it ON i.\"Id\" = it.\"InstanceId\"
    GROUP BY i.\"Id\"
) counts
ORDER BY \"Schema\";" \
"table" 6 12 12 6

# 5. Recent Activity - Last 7 Days
create_question "Recent Activity - Last 7 Days" \
"SELECT 
    'sys-flows' as \"Schema\",
    DATE(\"CreatedAt\") as \"Date\",
    COUNT(*) as \"New Instances\"
FROM sys_flows.\"Instances\"
WHERE \"CreatedAt\" >= CURRENT_DATE - INTERVAL '7 days'
GROUP BY DATE(\"CreatedAt\")
UNION ALL
SELECT 
    'sys-functions' as \"Schema\",
    DATE(\"CreatedAt\") as \"Date\",
    COUNT(*) as \"New Instances\"
FROM sys_functions.\"Instances\"
WHERE \"CreatedAt\" >= CURRENT_DATE - INTERVAL '7 days'
GROUP BY DATE(\"CreatedAt\")
UNION ALL
SELECT 
    'sys-tasks' as \"Schema\",
    DATE(\"CreatedAt\") as \"Date\",
    COUNT(*) as \"New Instances\"
FROM sys_tasks.\"Instances\"
WHERE \"CreatedAt\" >= CURRENT_DATE - INTERVAL '7 days'
GROUP BY DATE(\"CreatedAt\")
ORDER BY \"Date\" DESC, \"Schema\";" \
"line" 12 0 12 6

# 6. Flow Performance - Average Duration
create_question "Flow Performance - Average Duration" \
"SELECT 
    'sys-flows' as \"Schema\",
    \"Flow\",
    COUNT(*) as \"Instance Count\",
    AVG(EXTRACT(EPOCH FROM \"Duration\")) as \"Avg Duration (seconds)\"
FROM sys_flows.\"Instances\"
WHERE \"Duration\" IS NOT NULL
GROUP BY \"Flow\"
ORDER BY \"Avg Duration (seconds)\" DESC
LIMIT 10;" \
"table" 12 12 12 6

# 7. Total System Summary
create_question "Total System Summary" \
"SELECT 
    'Total Instances' as \"Metric\",
    (
        (SELECT COUNT(*) FROM sys_flows.\"Instances\") +
        (SELECT COUNT(*) FROM sys_functions.\"Instances\") +
        (SELECT COUNT(*) FROM sys_schemas.\"Instances\") +
        (SELECT COUNT(*) FROM sys_tasks.\"Instances\") +
        (SELECT COUNT(*) FROM sys_views.\"Instances\") +
        (SELECT COUNT(*) FROM sys_extensions.\"Instances\") +
        (SELECT COUNT(*) FROM checking_account_opening.\"Instances\")
    ) as \"Value\"
UNION ALL
SELECT 
    'Active Instances' as \"Metric\",
    (
        (SELECT COUNT(*) FROM sys_flows.\"Instances\" WHERE \"Status\" = 'Active') +
        (SELECT COUNT(*) FROM sys_functions.\"Instances\" WHERE \"Status\" = 'Active') +
        (SELECT COUNT(*) FROM sys_schemas.\"Instances\" WHERE \"Status\" = 'Active') +
        (SELECT COUNT(*) FROM sys_tasks.\"Instances\" WHERE \"Status\" = 'Active') +
        (SELECT COUNT(*) FROM sys_views.\"Instances\" WHERE \"Status\" = 'Active') +
        (SELECT COUNT(*) FROM sys_extensions.\"Instances\" WHERE \"Status\" = 'Active') +
        (SELECT COUNT(*) FROM checking_account_opening.\"Instances\" WHERE \"Status\" = 'Active')
    ) as \"Value\"
UNION ALL
SELECT 
    'Completed Instances' as \"Metric\",
    (
        (SELECT COUNT(*) FROM sys_flows.\"Instances\" WHERE \"Status\" = 'Completed') +
        (SELECT COUNT(*) FROM sys_functions.\"Instances\" WHERE \"Status\" = 'Completed') +
        (SELECT COUNT(*) FROM sys_schemas.\"Instances\" WHERE \"Status\" = 'Completed') +
        (SELECT COUNT(*) FROM sys_tasks.\"Instances\" WHERE \"Status\" = 'Completed') +
        (SELECT COUNT(*) FROM sys_views.\"Instances\" WHERE \"Status\" = 'Completed') +
        (SELECT COUNT(*) FROM sys_extensions.\"Instances\" WHERE \"Status\" = 'Completed') +
        (SELECT COUNT(*) FROM checking_account_opening.\"Instances\" WHERE \"Status\" = 'Completed')
    ) as \"Value\";" \
"table" 18 0 12 6

echo ""
echo "🎉 Comprehensive dashboard created successfully!"
echo "🔗 Dashboard URL: http://localhost:3001/dashboard/$DASHBOARD_ID"
echo ""
echo "📊 Dashboard includes:"
echo "  ✅ Schema overview with instance counts"
echo "  ✅ Status distribution (Active vs Completed)"
echo "  ✅ InstanceData count analysis per instance"
echo "  ✅ InstanceTransition count analysis per instance"
echo "  ✅ Recent activity (last 7 days)"
echo "  ✅ Flow performance analysis"
echo "  ✅ Total system summary"
echo ""
echo "🚀 Open the dashboard to see your comprehensive vNext system analysis!"
