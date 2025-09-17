#!/bin/bash

# Simple Metabase Dashboard Creation
# Bu script direkt API çağrıları ile basit dashboard oluşturur

echo "🎯 Simple vNext Dashboard Creator"
echo "=================================="

# Metabase credentials - modify these
METABASE_URL="http://localhost:3001"
EMAIL="tsimsek@burgan.com.tr"
# Password will be prompted

echo -n "Metabase Password: "
read -s PASSWORD
echo ""

# Get session token
echo "🔑 Getting session token..."
SESSION_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -d "{\"username\":\"$EMAIL\",\"password\":\"$PASSWORD\"}" \
    "$METABASE_URL/api/session")

SESSION_TOKEN=$(echo $SESSION_RESPONSE | grep -o '"id":"[^"]*"' | cut -d'"' -f4)

if [ -z "$SESSION_TOKEN" ]; then
    echo "❌ Login failed"
    exit 1
fi

echo "✅ Login successful!"

# Create first question - Total Instances
echo "📊 Creating Total Instances question..."

QUESTION1_JSON='{
  "name": "Total Instances",
  "dataset_query": {
    "type": "native",
    "native": {
      "query": "SELECT COUNT(*) as total FROM sys_flows.\"Instances\";"
    },
    "database": 1
  },
  "display": "scalar",
  "visualization_settings": {}
}'

Q1_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d "$QUESTION1_JSON" \
    "$METABASE_URL/api/card")

Q1_ID=$(echo $Q1_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)

if [ -n "$Q1_ID" ]; then
    echo "✅ Question 1 created: ID $Q1_ID"
else
    echo "❌ Question 1 failed: $Q1_RESPONSE"
fi

# Create second question - Status Distribution
echo "📊 Creating Status Distribution question..."

QUESTION2_JSON='{
  "name": "Status Distribution",
  "dataset_query": {
    "type": "native", 
    "native": {
      "query": "SELECT \"Status\", COUNT(*) as count FROM sys_flows.\"Instances\" GROUP BY \"Status\";"
    },
    "database": 1
  },
  "display": "pie",
  "visualization_settings": {}
}'

Q2_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d "$QUESTION2_JSON" \
    "$METABASE_URL/api/card")

Q2_ID=$(echo $Q2_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)

if [ -n "$Q2_ID" ]; then
    echo "✅ Question 2 created: ID $Q2_ID"
else
    echo "❌ Question 2 failed: $Q2_RESPONSE"
fi

# Create Dashboard
echo "📋 Creating dashboard..."

DASHBOARD_JSON='{
  "name": "vNext System Overview",
  "description": "Basic vNext workflow metrics"
}'

DASHBOARD_RESPONSE=$(curl -s -X POST \
    -H "Content-Type: application/json" \
    -H "X-Metabase-Session: $SESSION_TOKEN" \
    -d "$DASHBOARD_JSON" \
    "$METABASE_URL/api/dashboard")

DASHBOARD_ID=$(echo $DASHBOARD_RESPONSE | grep -o '"id":[0-9]*' | cut -d':' -f2)

if [ -n "$DASHBOARD_ID" ]; then
    echo "✅ Dashboard created: ID $DASHBOARD_ID"
    
    # Add cards to dashboard if questions were created
    if [ -n "$Q1_ID" ] && [ -n "$Q2_ID" ]; then
        echo "📋 Adding questions to dashboard..."
        
        CARDS_JSON="[{\"id\":-1,\"card_id\":$Q1_ID,\"row\":0,\"col\":0,\"size_x\":6,\"size_y\":4},{\"id\":-2,\"card_id\":$Q2_ID,\"row\":0,\"col\":6,\"size_x\":6,\"size_y\":4}]"
        
        curl -s -X PUT \
            -H "Content-Type: application/json" \
            -H "X-Metabase-Session: $SESSION_TOKEN" \
            -d "{\"cards\":$CARDS_JSON}" \
            "$METABASE_URL/api/dashboard/$DASHBOARD_ID" > /dev/null
        
        echo "✅ Questions added to dashboard!"
    fi
    
    echo ""
    echo "🎉 Dashboard created successfully!"
    echo "🔗 URL: $METABASE_URL/dashboard/$DASHBOARD_ID"
    
else
    echo "❌ Dashboard creation failed: $DASHBOARD_RESPONSE"
fi

echo ""
echo "✅ Process completed!"
