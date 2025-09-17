#!/bin/bash

echo "🚀 Simple Comprehensive Dashboard Creator"
echo "========================================"

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

# Create dashboard
echo "📋 Creating dashboard..."

DASHBOARD_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d '{
        "name": "vNext System Analysis",
        "description": "Complete system analysis dashboard"
    }' \
    "http://localhost:3001/api/dashboard")

DASHBOARD_ID=$(echo $DASHBOARD_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)

if [ -z "$DASHBOARD_ID" ]; then
    echo "❌ Failed to create dashboard: $DASHBOARD_RESPONSE"
    exit 1
fi

echo "✅ Dashboard created with ID: $DASHBOARD_ID"

# Create questions one by one
echo ""
echo "📊 Creating analysis questions..."

# 1. Total Instances by Schema
echo "Creating: Total Instances by Schema"
QUESTION1_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d '{
        "name": "Total Instances by Schema",
        "description": "Count of instances in each schema",
        "dataset_query": {
            "type": "native",
            "native": {
                "query": "SELECT '\''sys-flows'\'' as schema_name, COUNT(*) as instance_count FROM sys_flows.\"Instances\" UNION ALL SELECT '\''sys-functions'\'' as schema_name, COUNT(*) as instance_count FROM sys_functions.\"Instances\" UNION ALL SELECT '\''sys-schemas'\'' as schema_name, COUNT(*) as instance_count FROM sys_schemas.\"Instances\" UNION ALL SELECT '\''sys-tasks'\'' as schema_name, COUNT(*) as instance_count FROM sys_tasks.\"Instances\" UNION ALL SELECT '\''sys-views'\'' as schema_name, COUNT(*) as instance_count FROM sys_views.\"Instances\" UNION ALL SELECT '\''sys-extensions'\'' as schema_name, COUNT(*) as instance_count FROM sys_extensions.\"Instances\" UNION ALL SELECT '\''checking_account_opening'\'' as schema_name, COUNT(*) as instance_count FROM checking_account_opening.\"Instances\" ORDER BY instance_count DESC;"
            },
            "database": 2
        },
        "display": "table",
        "visualization_settings": {}
    }' \
    "http://localhost:3001/api/card")

QUESTION1_ID=$(echo $QUESTION1_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)

if [ -n "$QUESTION1_ID" ]; then
    echo "✅ Question 1 created: ID $QUESTION1_ID"
    
    # Add to dashboard
    curl -s -X PUT \
        -H "Content-Type: application/json" \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        -d '{
            "cards": [{
                "id": -1,
                "card_id": '$QUESTION1_ID',
                "row": 0,
                "col": 0,
                "size_x": 6,
                "size_y": 4,
                "series": [],
                "visualization_settings": {},
                "parameter_mappings": []
            }]
        }' \
        "http://localhost:3001/api/dashboard/$DASHBOARD_ID" > /dev/null
    echo "✅ Added to dashboard"
else
    echo "❌ Failed to create question 1: $QUESTION1_RESPONSE"
fi

# 2. Status Distribution
echo "Creating: Status Distribution"
QUESTION2_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d '{
        "name": "Status Distribution",
        "description": "Active vs Completed instances",
        "dataset_query": {
            "type": "native",
            "native": {
                "query": "SELECT '\''Active'\'' as status, (SELECT COUNT(*) FROM sys_flows.\"Instances\" WHERE \"Status\" = '\''Active'\'') + (SELECT COUNT(*) FROM sys_functions.\"Instances\" WHERE \"Status\" = '\''Active'\'') + (SELECT COUNT(*) FROM sys_schemas.\"Instances\" WHERE \"Status\" = '\''Active'\'') + (SELECT COUNT(*) FROM sys_tasks.\"Instances\" WHERE \"Status\" = '\''Active'\'') + (SELECT COUNT(*) FROM sys_views.\"Instances\" WHERE \"Status\" = '\''Active'\'') + (SELECT COUNT(*) FROM sys_extensions.\"Instances\" WHERE \"Status\" = '\''Active'\'') + (SELECT COUNT(*) FROM checking_account_opening.\"Instances\" WHERE \"Status\" = '\''Active'\'') as count UNION ALL SELECT '\''Completed'\'' as status, (SELECT COUNT(*) FROM sys_flows.\"Instances\" WHERE \"Status\" = '\''Completed'\'') + (SELECT COUNT(*) FROM sys_functions.\"Instances\" WHERE \"Status\" = '\''Completed'\'') + (SELECT COUNT(*) FROM sys_schemas.\"Instances\" WHERE \"Status\" = '\''Completed'\'') + (SELECT COUNT(*) FROM sys_tasks.\"Instances\" WHERE \"Status\" = '\''Completed'\'') + (SELECT COUNT(*) FROM sys_views.\"Instances\" WHERE \"Status\" = '\''Completed'\'') + (SELECT COUNT(*) FROM sys_extensions.\"Instances\" WHERE \"Status\" = '\''Completed'\'') + (SELECT COUNT(*) FROM checking_account_opening.\"Instances\" WHERE \"Status\" = '\''Completed'\'') as count;"
            },
            "database": 2
        },
        "display": "pie",
        "visualization_settings": {}
    }' \
    "http://localhost:3001/api/card")

QUESTION2_ID=$(echo $QUESTION2_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)

if [ -n "$QUESTION2_ID" ]; then
    echo "✅ Question 2 created: ID $QUESTION2_ID"
    
    # Add to dashboard
    curl -s -X PUT \
        -H "Content-Type: application/json" \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        -d '{
            "cards": [{
                "id": -1,
                "card_id": '$QUESTION2_ID',
                "row": 0,
                "col": 6,
                "size_x": 6,
                "size_y": 4,
                "series": [],
                "visualization_settings": {},
                "parameter_mappings": []
            }]
        }' \
        "http://localhost:3001/api/dashboard/$DASHBOARD_ID" > /dev/null
    echo "✅ Added to dashboard"
else
    echo "❌ Failed to create question 2: $QUESTION2_RESPONSE"
fi

# 3. InstanceData Count Analysis
echo "Creating: InstanceData Count Analysis"
QUESTION3_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d '{
        "name": "InstanceData Count Analysis",
        "description": "Average InstanceData count per instance",
        "dataset_query": {
            "type": "native",
            "native": {
                "query": "SELECT '\''sys-flows'\'' as schema_name, AVG(data_count) as avg_data_count FROM (SELECT i.\"Id\", COUNT(id.\"Id\") as data_count FROM sys_flows.\"Instances\" i LEFT JOIN sys_flows.\"InstancesData\" id ON i.\"Id\" = id.\"InstanceId\" GROUP BY i.\"Id\") counts UNION ALL SELECT '\''sys-functions'\'' as schema_name, AVG(data_count) as avg_data_count FROM (SELECT i.\"Id\", COUNT(id.\"Id\") as data_count FROM sys_functions.\"Instances\" i LEFT JOIN sys_functions.\"InstancesData\" id ON i.\"Id\" = id.\"InstanceId\" GROUP BY i.\"Id\") counts UNION ALL SELECT '\''sys-tasks'\'' as schema_name, AVG(data_count) as avg_data_count FROM (SELECT i.\"Id\", COUNT(id.\"Id\") as data_count FROM sys_tasks.\"Instances\" i LEFT JOIN sys_tasks.\"InstancesData\" id ON i.\"Id\" = id.\"InstanceId\" GROUP BY i.\"Id\") counts ORDER BY avg_data_count DESC;"
            },
            "database": 2
        },
        "display": "table",
        "visualization_settings": {}
    }' \
    "http://localhost:3001/api/card")

QUESTION3_ID=$(echo $QUESTION3_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)

if [ -n "$QUESTION3_ID" ]; then
    echo "✅ Question 3 created: ID $QUESTION3_ID"
    
    # Add to dashboard
    curl -s -X PUT \
        -H "Content-Type: application/json" \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        -d '{
            "cards": [{
                "id": -1,
                "card_id": '$QUESTION3_ID',
                "row": 4,
                "col": 0,
                "size_x": 6,
                "size_y": 4,
                "series": [],
                "visualization_settings": {},
                "parameter_mappings": []
            }]
        }' \
        "http://localhost:3001/api/dashboard/$DASHBOARD_ID" > /dev/null
    echo "✅ Added to dashboard"
else
    echo "❌ Failed to create question 3: $QUESTION3_RESPONSE"
fi

# 4. Recent Activity
echo "Creating: Recent Activity"
QUESTION4_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d '{
        "name": "Recent Activity - Last 7 Days",
        "description": "New instances created in last 7 days",
        "dataset_query": {
            "type": "native",
            "native": {
                "query": "SELECT DATE(\"CreatedAt\") as date, COUNT(*) as new_instances FROM sys_flows.\"Instances\" WHERE \"CreatedAt\" >= CURRENT_DATE - INTERVAL '\''7 days'\'' GROUP BY DATE(\"CreatedAt\") UNION ALL SELECT DATE(\"CreatedAt\") as date, COUNT(*) as new_instances FROM sys_functions.\"Instances\" WHERE \"CreatedAt\" >= CURRENT_DATE - INTERVAL '\''7 days'\'' GROUP BY DATE(\"CreatedAt\") UNION ALL SELECT DATE(\"CreatedAt\") as date, COUNT(*) as new_instances FROM sys_tasks.\"Instances\" WHERE \"CreatedAt\" >= CURRENT_DATE - INTERVAL '\''7 days'\'' GROUP BY DATE(\"CreatedAt\") ORDER BY date DESC;"
            },
            "database": 2
        },
        "display": "line",
        "visualization_settings": {}
    }' \
    "http://localhost:3001/api/card")

QUESTION4_ID=$(echo $QUESTION4_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)

if [ -n "$QUESTION4_ID" ]; then
    echo "✅ Question 4 created: ID $QUESTION4_ID"
    
    # Add to dashboard
    curl -s -X PUT \
        -H "Content-Type: application/json" \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        -d '{
            "cards": [{
                "id": -1,
                "card_id": '$QUESTION4_ID',
                "row": 4,
                "col": 6,
                "size_x": 6,
                "size_y": 4,
                "series": [],
                "visualization_settings": {},
                "parameter_mappings": []
            }]
        }' \
        "http://localhost:3001/api/dashboard/$DASHBOARD_ID" > /dev/null
    echo "✅ Added to dashboard"
else
    echo "❌ Failed to create question 4: $QUESTION4_RESPONSE"
fi

echo ""
echo "🎉 Simple comprehensive dashboard created successfully!"
echo "🔗 Dashboard URL: http://localhost:3001/dashboard/$DASHBOARD_ID"
echo ""
echo "📊 Dashboard includes:"
echo "  ✅ Total instances by schema"
echo "  ✅ Status distribution (Active vs Completed)"
echo "  ✅ InstanceData count analysis"
echo "  ✅ Recent activity (last 7 days)"
echo ""
echo "🚀 Open the dashboard to see your vNext system analysis!"

