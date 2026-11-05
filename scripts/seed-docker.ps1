# Seed database for Docker Compose environment
# Usage: .\scripts\seed-docker.ps1

Write-Host "Checking SQL Server container status..." -ForegroundColor Cyan
$container = docker ps --filter "name=acadprep-sqlserver" --format "{{.Status}}"
if (-not $container) {
    Write-Host "Container 'acadprep-sqlserver' is not running. Please start docker-compose first:" -ForegroundColor Yellow
    Write-Host "docker compose up -d" -ForegroundColor White
    exit 1
}

Write-Host "SQL Server container is running ($container)." -ForegroundColor Green
Write-Host "Running Demo Data Seeder against local SQL Server (port 1433)..." -ForegroundColor Cyan

dotnet run --project tools/SeedDemoData -- --local-sql

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nDatabase seeded successfully!" -ForegroundColor Green
    Write-Host "Accounts available:" -ForegroundColor White
    Write-Host "  - Admin:     admin@acadprep.com            / Admin@123456"
    Write-Host "  - Moderator: moderator@acadprep.com        / Moderator@123456"
    Write-Host "  - Moderator: moderator.demo@example.test   / Demo@123456"
    Write-Host "  - Learner:   learner@acadprep.com          / Learner@123456"
    Write-Host "  - Learner:   hana@acadprep.com             / Demo@123456"
} else {
    Write-Host "`nFailed to seed database. Please check logs above." -ForegroundColor Red
}

