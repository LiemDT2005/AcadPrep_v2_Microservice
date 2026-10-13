# PowerShell script to stop running microservices
Write-Host "Stopping dotnet processes..." -ForegroundColor Yellow
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Host "All dotnet services stopped." -ForegroundColor Green
