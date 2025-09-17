#!/bin/bash

# vNext Metabase Setup Script
# Bu script Metabase kurulumunu ve temel konfigürasyonu otomatik yapar

set -e

echo "🚀 vNext Metabase Setup başlatılıyor..."

# Color definitions for better output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function definitions
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

# Check if Docker is running
check_docker() {
    if ! docker info > /dev/null 2>&1; then
        log_error "Docker is not running. Please start Docker first."
        exit 1
    fi
    log_success "Docker is running"
}

# Check if docker-compose is available
check_docker_compose() {
    if ! command -v docker-compose &> /dev/null; then
        log_error "docker-compose is not installed or not in PATH"
        exit 1
    fi
    log_success "docker-compose is available"
}

# Create external network if not exists
create_network() {
    log_info "Creating external network 'bbt-development' if not exists..."
    if ! docker network ls | grep -q "bbt-development"; then
        docker network create bbt-development
        log_success "Network 'bbt-development' created"
    else
        log_info "Network 'bbt-development' already exists"
    fi
}

# Start services
start_services() {
    log_info "Starting services with docker-compose..."
    cd "$(dirname "$0")/../etc/docker"
    
    # Start base services first (postgres, redis, etc.)
    docker-compose up -d postgres redis vault dapr-placement dapr-scheduler
    log_success "Base services started"
    
    # Wait for postgres to be ready
    log_info "Waiting for PostgreSQL to be ready..."
    while ! docker exec vnext-postgres pg_isready -U postgres &> /dev/null; do
        sleep 2
        echo -n "."
    done
    echo ""
    log_success "PostgreSQL is ready"
    
    # Create metabase database
    log_info "Creating Metabase database..."
    docker exec vnext-postgres psql -U postgres -c "CREATE DATABASE metabase;" 2>/dev/null || \
    docker exec vnext-postgres psql -U postgres -c "SELECT 'Database already exists' WHERE EXISTS(SELECT 1 FROM pg_database WHERE datname='metabase');" >/dev/null
    log_success "Metabase database ready"
    
    # Start metabase
    log_info "Starting Metabase..."
    docker-compose up -d metabase
    
    # Wait for metabase to be ready
    log_info "Waiting for Metabase to be ready (this may take a few minutes)..."
    while ! curl -f http://localhost:3001/api/health &> /dev/null; do
        sleep 10
        echo -n "."
    done
    echo ""
    log_success "Metabase is ready!"
}

# Create database indexes for better performance
create_indexes() {
    log_info "Creating database indexes for better performance..."
    
    # Array of schemas
    schemas=("sys_flows" "sys_functions" "sys_schemas" "sys_tasks" "sys_views" "sys_extensions")
    
    for schema in "${schemas[@]}"; do
        log_info "Creating indexes for schema: $schema"
        
        # Create schema if not exists
        docker exec vnext-postgres psql -U postgres -c "CREATE SCHEMA IF NOT EXISTS \"$schema\";" || true
        
        # Check if Instances table exists
        table_exists=$(docker exec vnext-postgres psql -U postgres -t -c "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = '$schema' AND table_name = 'Instances');")
        
        if [[ "$table_exists" == *"t"* ]]; then
            # Create performance indexes
            docker exec vnext-postgres psql -U postgres -c "
                CREATE INDEX IF NOT EXISTS idx_${schema}_instances_flow_status_created 
                ON \"$schema\".\"Instances\" (\"Flow\", \"Status\", \"CreatedAt\");
                
                CREATE INDEX IF NOT EXISTS idx_${schema}_instances_key_created 
                ON \"$schema\".\"Instances\" (\"Key\", \"CreatedAt\");
            " || true
            
            # Check if InstancesData table exists
            data_table_exists=$(docker exec vnext-postgres psql -U postgres -t -c "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = '$schema' AND table_name = 'InstancesData');")
            
            if [[ "$data_table_exists" == *"t"* ]]; then
                docker exec vnext-postgres psql -U postgres -c "
                    CREATE INDEX IF NOT EXISTS idx_${schema}_instancesdata_instance_latest 
                    ON \"$schema\".\"InstancesData\" (\"InstanceId\", \"IsLatest\");
                    
                    CREATE INDEX IF NOT EXISTS idx_${schema}_instancesdata_type 
                    ON \"$schema\".\"InstancesData\" USING gin ((\"Data\" ->> 'Type'));
                " || true
            fi
            
            log_success "Indexes created for $schema"
        else
            log_warning "Instances table not found for schema $schema, skipping index creation"
        fi
    done
}

# Display connection information
display_info() {
    log_success "🎉 Metabase setup completed successfully!"
    echo ""
    echo "📊 Access Information:"
    echo "  Metabase URL: http://localhost:3001"
    echo "  PostgreSQL: localhost:5432"
    echo "  PgAdmin: http://localhost:5502 (admin/admin)"
    echo "  Redis: localhost:6379"
    echo "  Redis Insight: http://localhost:5501"
    echo ""
    echo "🔧 Database Connection for Metabase:"
    echo "  Database Type: PostgreSQL"
    echo "  Host: postgres"
    echo "  Port: 5432"
    echo "  Database: postgres"
    echo "  Username: postgres"
    echo "  Password: postgres"
    echo ""
    echo "📁 Dashboard Files Location:"
    echo "  /metabase-dashboards/ (mounted in container)"
    echo "  $(pwd)/../metabase-dashboards/ (host path)"
    echo ""
    log_info "Please follow the setup-instructions.md for dashboard import process"
}

# Main execution
main() {
    echo "🎯 vNext Metabase Setup Script"
    echo "=============================="
    
    check_docker
    check_docker_compose
    create_network
    start_services
    create_indexes
    display_info
    
    log_success "Setup script completed! 🚀"
}

# Error handling
trap 'log_error "An error occurred. Exiting..."; exit 1' ERR

# Run main function
main "$@"
