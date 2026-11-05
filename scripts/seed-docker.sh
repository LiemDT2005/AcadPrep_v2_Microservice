#!/usr/bin/env bash
# Seed database for Docker Compose environment
# Usage: ./scripts/seed-docker.sh

echo "Checking SQL Server container status..."
if ! docker ps --filter "name=acadprep-sqlserver" --format "{{.Status}}" | grep -q "Up"; then
    echo "Container 'acadprep-sqlserver' is not running. Please start docker-compose first:"
    echo "docker compose up -d"
    exit 1
fi

echo "SQL Server container is running."
echo "Running Demo Data Seeder against local SQL Server (port 1433)..."

dotnet run --project tools/SeedDemoData -- --local-sql

if [ $? -eq 0 ]; then
    echo ""
    echo "Database seeded successfully!"
    echo "Accounts available:"
    echo "  - Admin:     admin@acadprep.com            / Admin@123456"
    echo "  - Moderator: moderator@acadprep.com        / Moderator@123456"
    echo "  - Moderator: moderator.demo@example.test   / Demo@123456"
    echo "  - Learner:   learner@acadprep.com          / Learner@123456"
    echo "  - Learner:   hana@acadprep.com             / Demo@123456"
else
    echo "Failed to seed database. Please check logs above."
fi
