#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "==> Starting SQL Server + RabbitMQ + Redis"
docker compose up -d sqlserver rabbitmq redis

wait_for() {
  local name="$1"
  shift
  echo "==> Waiting for $name..."
  for attempt in {1..60}; do
    if "$@" >/dev/null 2>&1; then return 0; fi
    sleep 2
  done
  echo "ERROR: $name is not ready. Check docker compose logs." >&2
  exit 1
}

wait_for "SQL Server" docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Password123!' -C -Q 'SELECT 1'
wait_for "RabbitMQ" docker compose exec -T rabbitmq rabbitmq-diagnostics -q ping
wait_for "Redis" docker compose exec -T redis redis-cli ping

pids=()
cleanup() {
  trap - EXIT INT TERM
  if ((${#pids[@]})); then
    kill "${pids[@]}" 2>/dev/null || true
    wait "${pids[@]}" 2>/dev/null || true
  fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

export ASPNETCORE_ENVIRONMENT=Development

run() {
  local name="$1"
  local path="$2"
  echo "==> Starting $name"
  (cd "$path" && exec dotnet run --no-launch-profile) &
  pids+=("$!")
}

run "Identity.API" "src/Services/Identity/Identity.API"
run "Exam" "src/Services/Exam"
run "Learning.API" "src/Services/Learning/Learning.API"
run "Content" "src/Services/Content"
run "ApiGateway" "src/ApiGateways/ApiGateway"
run "Storefront.Web" "src/Web/Storefront.Web"

echo ""
echo "Services starting..."
echo "  Web UI:     http://localhost:5100"
echo "  Gateway:    http://localhost:5080"
echo "  Exam:       http://localhost:5003/swagger"
echo "  Learning:   http://localhost:5005/swagger"
echo "  Content:    http://localhost:5006/swagger"
echo "  RabbitMQ:   http://localhost:15672 (guest/guest)"
echo ""
echo "Press Ctrl+C to stop all."
wait
