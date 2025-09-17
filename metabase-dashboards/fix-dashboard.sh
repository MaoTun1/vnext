#!/bin/bash

echo "🔧 Dashboard Fix & Test Script"
echo "=============================="

# Test database connection first
echo "🧪 Testing database connection..."

read -p "Metabase Email: " EMAIL
read -s -p "Metabase Password: " PASSWORD
echo ""

# Get session
SESSION_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -d "{\"username\":\"$EMAIL\",\"password\":\"$PASSWORD\"}" \
    "http://localhost:3001/api/session")

SESSION_TOKEN=$(echo $SESSION_RESPONSE | grep -o '"id":"[^"]*"' | cut -d'"' -f4)

if [ -z "$SESSION_TOKEN" ]; then
    echo "❌ Login failed"
    exit 1
fi

echo "✅ Login successful!"

# Test database query directly
echo "🧪 Testing database query..."

TEST_QUERY='{
  "type": "native",
  "native": {
    "query": "SELECT COUNT(*) as total FROM sys_flows.\"Instances\";"
  },
  "database": 1
}'

TEST_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d "$TEST_QUERY" \
    "http://localhost:3001/api/dataset")

echo "Query Response: $TEST_RESPONSE"

# Check current dashboard
echo "🔍 Checking dashboard 3..."

DASHBOARD_RESPONSE=$(curl -s -H "X-Metabase-Session: $SESSION_TOKEN" \
    "http://localhost:3001/api/dashboard/3")

echo "Dashboard info: $DASHBOARD_RESPONSE"

# Create new working dashboard
echo "🆕 Creating new working dashboard..."

# Delete old questions first (if they exist but are broken)
echo "🗑️  Cleaning old questions..."

# Create new question with proper format
WORKING_QUESTION='{
  "name": "Working Total Count",
  "description": "Total count of instances", 
  "dataset_query": {
    "type": "native",
    "native": {
      "query": "SELECT COUNT(*) as \"Total Instances\" FROM sys_flows.\"Instances\";"
    },
    "database": 1
  },
  "display": "scalar",
  "visualization_settings": {
    "scalar.field": "Total Instances"
  }
}'

QUESTION_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d "$WORKING_QUESTION" \
    "http://localhost:3001/api/card")

QUESTION_ID=$(echo $QUESTION_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)

if [ -n "$QUESTION_ID" ]; then
    echo "✅ Working question created: ID $QUESTION_ID"
    
    # Create clean dashboard
    CLEAN_DASHBOARD='{
      "name": "vNext Working Dashboard",
      "description": "Clean working dashboard with proper cards"
    }'
    
    DASHBOARD_RESPONSE=$(curl -s -X POST \
        -H "Content-Type: application/json" \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        -d "$CLEAN_DASHBOARD" \
        "http://localhost:3001/api/dashboard")
    
    NEW_DASHBOARD_ID=$(echo $DASHBOARD_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)
    
    if [ -n "$NEW_DASHBOARD_ID" ]; then
        echo "✅ Clean dashboard created: ID $NEW_DASHBOARD_ID"
        
        # Add working question to dashboard
        DASHBOARD_CARDS='[{
          "id": -1,
          "card_id": '$QUESTION_ID',
          "row": 0,
          "col": 0,
          "size_x": 6,
          "size_y": 4,
          "series": [],
          "visualization_settings": {},
          "parameter_mappings": []
        }]'
        
        curl -s -X PUT \
            -H "Content-Type: application/json" \
            -H "X-Metabase-Session: $SESSION_TOKEN" \
            -d "{\"cards\":$DASHBOARD_CARDS}" \
            "http://localhost:3001/api/dashboard/$NEW_DASHBOARD_ID"
        
        echo "✅ Working dashboard completed!"
        echo "🔗 New Dashboard URL: http://localhost:3001/dashboard/$NEW_DASHBOARD_ID"
        
    else
        echo "❌ Failed to create dashboard: $DASHBOARD_RESPONSE"
    fi
else
    echo "❌ Failed to create question: $QUESTION_RESPONSE"
fi

echo ""
echo "✅ Fix script completed!"


