#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "==> Starting RabbitMQ + Redis"
docker compose up -d

echo "==> Waiting for RabbitMQ..."
sleep 5

export ASPNETCORE_ENVIRONMENT=Development

run() {
  local name="$1"
  local path="$2"
  echo "==> Starting $name"
  (cd "$path" && dotnet run --no-launch-profile) &
}

run "Identity.API" "src/Services/Identity/Identity.API"
run "Catalog.API" "src/Services/Catalog/Catalog.API"
run "Exam.API" "src/Services/Exam/Exam.API"
run "Progress.API" "src/Services/Progress/Progress.API"
run "Learning.API" "src/Services/Learning/Learning.API"
run "ApiGateway" "src/ApiGateways/ApiGateway"
run "Storefront.Web" "src/Web/Storefront.Web"

echo ""
echo "Services starting..."
echo "  Web UI:     http://localhost:5100"
echo "  Gateway:    http://localhost:5080"
echo "  Identity:   http://localhost:5001/swagger"
echo "  Catalog:    http://localhost:5002/swagger"
echo "  Exam:       http://localhost:5003/swagger"
echo "  Progress:   http://localhost:5004/swagger"
echo "  Learning:   http://localhost:5005/swagger"
echo "  RabbitMQ:   http://localhost:15672 (guest/guest)"
echo ""
echo "Press Ctrl+C to stop all."
wait
