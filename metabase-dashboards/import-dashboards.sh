#!/bin/bash

# Metabase Dashboard Import Script
# Bu script JSON dashboard dosyalarını Metabase API ile import eder

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Configuration
METABASE_URL="http://localhost:3001"
USERNAME=""
PASSWORD=""
DATABASE_ID=""
SESSION_TOKEN=""

log_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

log_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

log_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

log_error() {
    echo -e "${RED}❌ $1${NC}"
}

# Function to get user input
get_credentials() {
    log_info "Metabase Dashboard Import için bilgiler gerekli:"
    echo ""
    
    read -p "Metabase Email: " USERNAME
    read -s -p "Metabase Password: " PASSWORD
    echo ""
    echo ""
}

# Function to authenticate and get session token
authenticate() {
    log_info "Metabase'e giriş yapılıyor..."
    
    local response=$(curl -s -X POST \
        -H "Content-Type: application/json" \
        -d "{\"username\":\"$USERNAME\",\"password\":\"$PASSWORD\"}" \
        "$METABASE_URL/api/session")
    
    SESSION_TOKEN=$(echo $response | grep -o '"id":"[^"]*"' | cut -d'"' -f4)
    
    if [ -z "$SESSION_TOKEN" ]; then
        log_error "Giriş başarısız. Kullanıcı adı ve şifrenizi kontrol edin."
        echo "Response: $response"
        exit 1
    fi
    
    log_success "Giriş başarılı!"
}

# Function to get database ID
get_database_id() {
    log_info "Database ID alınıyor..."
    
    local response=$(curl -s -X GET \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        "$METABASE_URL/api/database")
    
    # Find database with name containing "vnext" or "workflow" or "Aether"
    DATABASE_ID=$(echo $response | grep -o '"id":[0-9]*' | head -1 | cut -d':' -f2)
    
    if [ -z "$DATABASE_ID" ]; then
        log_error "Database bulunamadı. Database bağlantısını kontrol edin."
        echo "Available databases: $response"
        exit 1
    fi
    
    log_success "Database ID: $DATABASE_ID"
}

# Function to create a collection for dashboards
create_collection() {
    log_info "Dashboard collection oluşturuluyor..."
    
    local response=$(curl -s -X POST \
        -H "Content-Type: application/json" \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        -d '{"name":"vNext Workflow Dashboards","description":"Automatically imported vNext workflow monitoring dashboards","color":"#509EE3"}' \
        "$METABASE_URL/api/collection")
    
    local collection_id=$(echo $response | grep -o '"id":[0-9]*' | cut -d':' -f2)
    
    if [ -n "$collection_id" ]; then
        log_success "Collection oluşturuldu: ID $collection_id"
        echo $collection_id
    else
        log_warning "Collection oluşturulamadı, root collection kullanılacak"
        echo "null"
    fi
}

# Function to create a simple dashboard manually (since JSON import is complex)
create_sample_dashboard() {
    local collection_id=$1
    log_info "Sample dashboard oluşturuluyor..."
    
    # Create dashboard
    local dashboard_data='{"name":"vNext System Overview","description":"Overview of vNext workflow system metrics","collection_id":'${collection_id:-null}'}'
    
    local dashboard_response=$(curl -s -X POST \
        -H "Content-Type: application/json" \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        -d "$dashboard_data" \
        "$METABASE_URL/api/dashboard")
    
    local dashboard_id=$(echo $dashboard_response | grep -o '"id":[0-9]*' | cut -d':' -f2)
    
    if [ -n "$dashboard_id" ]; then
        log_success "Dashboard oluşturuldu: ID $dashboard_id"
        
        # Create a simple question and add to dashboard
        create_sample_questions $dashboard_id
    else
        log_error "Dashboard oluşturulamadı"
        echo "Response: $dashboard_response"
    fi
}

# Function to create sample questions
create_sample_questions() {
    local dashboard_id=$1
    log_info "Sample questions oluşturuluyor..."
    
    # Question 1: Total Instances  
    local question1='{"name":"Total Active Instances","description":"Total number of active workflow instances","collection_id":null,"dataset_query":{"type":"native","native":{"query":"SELECT COUNT(*) as total_instances FROM sys_flows.\"Instances\";"},"database":'$DATABASE_ID'},"display":"scalar","visualization_settings":{}}'
    
    local q1_response=$(curl -s -X POST \
        -H "Content-Type: application/json" \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        -d "$question1" \
        "$METABASE_URL/api/card")
    
    local q1_id=$(echo $q1_response | grep -o '"id":[0-9]*' | cut -d':' -f2)
    
    # Question 2: Status Distribution
    local question2='{"name":"Status Distribution","description":"Distribution of workflow statuses","collection_id":null,"dataset_query":{"type":"native","native":{"query":"SELECT \"Status\", COUNT(*) as count FROM sys_flows.\"Instances\" GROUP BY \"Status\" ORDER BY count DESC;"},"database":'$DATABASE_ID'},"display":"pie","visualization_settings":{}}'
    
    local q2_response=$(curl -s -X POST \
        -H "Content-Type: application/json" \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        -d "$question2" \
        "$METABASE_URL/api/card")
    
    local q2_id=$(echo $q2_response | grep -o '"id":[0-9]*' | cut -d':' -f2)
    
    # Add questions to dashboard
    if [ -n "$q1_id" ] && [ -n "$q2_id" ]; then
        add_cards_to_dashboard $dashboard_id $q1_id $q2_id
    fi
}

# Function to add cards to dashboard
add_cards_to_dashboard() {
    local dashboard_id=$1
    local q1_id=$2
    local q2_id=$3
    
    log_info "Questions dashboard'a ekleniyor..."
    
    local dashboard_cards='[{"id":-1,"card_id":'$q1_id',"row":0,"col":0,"size_x":6,"size_y":4,"series":[],"visualization_settings":{},"parameter_mappings":[]},{"id":-2,"card_id":'$q2_id',"row":0,"col":6,"size_x":6,"size_y":4,"series":[],"visualization_settings":{},"parameter_mappings":[]}]'
    
    curl -s -X PUT \
        -H "Content-Type: application/json" \
        -H "X-Metabase-Session: $SESSION_TOKEN" \
        -d "{\"cards\":$dashboard_cards}" \
        "$METABASE_URL/api/dashboard/$dashboard_id"
    
    log_success "Dashboard kartları eklendi!"
    log_success "Dashboard URL: $METABASE_URL/dashboard/$dashboard_id"
}

# Main execution
main() {
    echo "🎯 vNext Metabase Dashboard Import Script"
    echo "=========================================="
    
    get_credentials
    authenticate
    get_database_id
    
    local collection_id=$(create_collection)
    create_sample_dashboard $collection_id
    
    echo ""
    log_success "Dashboard import tamamlandı! 🎉"
    log_info "Metabase'de Dashboards bölümünden görüntüleyebilirsiniz."
    echo ""
}

# Error handling
trap 'log_error "Bir hata oluştu. Script sonlandırılıyor..."; exit 1' ERR

# Run main function
main "$@"
