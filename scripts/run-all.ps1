# PowerShell runner for ToeicLearning Microservices
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

Write-Host "==> Starting RabbitMQ + Redis (Docker Compose)" -ForegroundColor Cyan
docker compose up -d

Start-Sleep -Seconds 3

$env:ASPNETCORE_ENVIRONMENT = "Development"

$services = @(
    @{ Name = "Identity.API"; Path = "src/Services/Identity/Identity.API" },
    @{ Name = "Catalog.API";  Path = "src/Services/Catalog/Catalog.API" },
    @{ Name = "Exam";     Path = "src/Services/Exam" },
    @{ Name = "Progress.API"; Path = "src/Services/Progress/Progress.API" },
    @{ Name = "Learning.API"; Path = "src/Services/Learning/Learning.API" },
    @{ Name = "Content";  Path = "src/Services/Content" },
    @{ Name = "ApiGateway";   Path = "src/ApiGateways/ApiGateway" },
    @{ Name = "Storefront.Web"; Path = "src/Web/Storefront.Web" }
)

Write-Host "==> Launching all services in background jobs..." -ForegroundColor Green
foreach ($s in $services) {
    Write-Host "Starting $($s.Name)..."
    Start-Process dotnet -ArgumentList "run --project $($s.Path) --no-launch-profile"
}

Write-Host ""
Write-Host "Microservices running:" -ForegroundColor Yellow
Write-Host "  Web UI:       http://localhost:5100"
Write-Host "  CMS Exams:    http://localhost:5100/Admin/Exams"
Write-Host "  Gateway:      http://localhost:5080"
Write-Host "  Identity:     http://localhost:5001/swagger"
Write-Host "  Catalog:      http://localhost:5002/swagger"
Write-Host "  Exam:         http://localhost:5003/swagger"
Write-Host "  Progress:     http://localhost:5004/swagger"
Write-Host "  Learning:     http://localhost:5005/swagger"
Write-Host "  Content:      http://localhost:5006/swagger"
Write-Host "  RabbitMQ:     http://localhost:15672 (guest/guest)"
Write-Host "  Gateway Content: http://localhost:5080/content/*"
Write-Host ""
