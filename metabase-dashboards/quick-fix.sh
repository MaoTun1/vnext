#!/bin/bash

echo "🔧 Quick Dashboard Fix"
echo "======================"

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

# Check available databases
echo "🔍 Checking available databases..."
DATABASES_RESPONSE=$(curl -s -H "X-Metabase-Session: $SESSION_TOKEN" \
    "http://localhost:3001/api/database")

echo "Available databases: $DATABASES_RESPONSE"

# Test database connection
echo "🧪 Testing database connection..."

TEST_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d '{
        "type": "native",
        "native": {
            "query": "SELECT COUNT(*) as total FROM sys_flows.\"Instances\";"
        },
        "database": 2
    }' \
    "http://localhost:3001/api/dataset")

echo "Database test result: $TEST_RESPONSE"

# Create a simple working question
echo "📊 Creating working question..."

QUESTION_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d '{
        "name": "Total Instances Count",
        "description": "Total number of instances in sys_flows",
        "dataset_query": {
            "type": "native",
            "native": {
                "query": "SELECT COUNT(*) as \"Total\" FROM sys_flows.\"Instances\";"
            },
            "database": 2
        },
        "display": "scalar",
        "visualization_settings": {
            "scalar.field": "Total"
        }
    }' \
    "http://localhost:3001/api/card")

QUESTION_ID=$(echo $QUESTION_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)

if [ -n "$QUESTION_ID" ]; then
    echo "✅ Question created with ID: $QUESTION_ID"
    
    # Create new dashboard
    echo "📋 Creating new dashboard..."
    
    DASHBOARD_RESPONSE=$(curl -s -X POST \
        -H "Content-Type: application/json" \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        -d '{
            "name": "vNext System Dashboard",
            "description": "Working dashboard for vNext system monitoring"
        }' \
        "http://localhost:3001/api/dashboard")
    
    DASHBOARD_ID=$(echo $DASHBOARD_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)
    
    if [ -n "$DASHBOARD_ID" ]; then
        echo "✅ Dashboard created with ID: $DASHBOARD_ID"
        
        # Add question to dashboard
        echo "🔗 Adding question to dashboard..."
        
        CARD_RESPONSE=$(curl -s -X PUT \
            -H "Content-Type: application/json" \
            -H "X-Metabase-Session: $SESSION_TOKEN" \
            -d '{
                "cards": [{
                    "id": -1,
                    "card_id": '$QUESTION_ID',
                    "row": 0,
                    "col": 0,
                    "size_x": 6,
                    "size_y": 4,
                    "series": [],
                    "visualization_settings": {},
                    "parameter_mappings": []
                }]
            }' \
            "http://localhost:3001/api/dashboard/$DASHBOARD_ID")
        
        echo "✅ Dashboard setup completed!"
        echo "🔗 Dashboard URL: http://localhost:3001/dashboard/$DASHBOARD_ID"
        echo ""
        echo "📝 Next steps:"
        echo "1. Open the dashboard URL above"
        echo "2. If it shows data, the issue is resolved"
        echo "3. If still empty, check database connection in Metabase admin"
        
    else
        echo "❌ Failed to create dashboard: $DASHBOARD_RESPONSE"
    fi
else
    echo "❌ Failed to create question: $QUESTION_RESPONSE"
fi

echo ""
echo "✅ Quick fix completed!"
