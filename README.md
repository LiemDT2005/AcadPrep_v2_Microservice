# TOEIC Learning Microservices

Project demo học TOEIC theo kiến trúc **microservices + Clean Architecture + Razor Pages**, sẵn luồng end-to-end để copy sang repo mới / làm đề tài.

## Công nghệ demo

| Thành phần | Công nghệ |
|---|---|
| Frontend | ASP.NET Core **Razor Pages** |
| API Gateway | **YARP** Reverse Proxy |
| Auth | ASP.NET Identity + **JWT** |
| Catalog / Exam / Progress | **Clean Architecture** (Domain → Application → Infrastructure → API) |
| Database | **SQLite** + EF Core (dễ chạy local) |
| Message bus | **RabbitMQ** (`ExamCompletedEvent`) |
| Cache | **Redis** (Progress summary) |
| Orchestration local | `docker compose` + `scripts/run-all.sh` |

## Cấu trúc solution

```text
ToeicLearning.Microservices/
├── ToeicLearning.sln
├── docker-compose.yml          # RabbitMQ + Redis
├── scripts/run-all.sh
├── src/
│   ├── BuildingBlocks/
│   │   ├── BuildingBlocks.Common      # Result, IntegrationEvent
│   │   └── BuildingBlocks.EventBus    # RabbitMQ publisher/subscriber
│   ├── Services/
│   │   ├── Identity/Identity.API
│   │   ├── Catalog/   (Domain, Application, Infrastructure, API)
│   │   ├── Exam/      (Domain, Application, Infrastructure, API)
│   │   └── Progress/  (Domain, Application, Infrastructure, API)
│   ├── ApiGateways/ApiGateway
│   └── Web/Storefront.Web             # Razor Pages UI
```

## Luồng demo (full stack)

```text
1. User đăng ký / đăng nhập trên Razor Pages
      → Identity.API cấp JWT (lưu Session)

2. Xem khóa học
      → Web → YARP `/catalog/*` → Catalog.API (Clean Arch + SQLite seed)

3. Làm đề Mini Part 5
      → Web → YARP `/exam/*` → Exam.API
      → chấm điểm, lưu ExamAttempt
      → Publish ExamCompletedEvent lên RabbitMQ

4. Progress.API subscribe event
      → ghi LearningRecord (SQLite)
      → invalidate / cache summary trên Redis

5. Trang Tiến độ
      → Web → YARP `/progress/*` → Progress.API (đọc DB + Redis)
```

## Ports

| Service | URL |
|---|---|
| Storefront.Web | http://localhost:5100 |
| ApiGateway | http://localhost:5080 |
| Identity.API | http://localhost:5001/swagger |
| Catalog.API | http://localhost:5002/swagger |
| Exam.API | http://localhost:5003/swagger |
| Progress.API | http://localhost:5004/swagger |
| RabbitMQ UI | http://localhost:15672 (`guest` / `guest`) |
| Redis | localhost:6379 |

## Cách chạy (hướng dẫn)

### Yêu cầu

- .NET SDK **9**
- Docker (cho RabbitMQ + Redis)

### Bước 1 — Clone / copy project

```bash
# copy cả thư mục ToeicLearning.Microservices sang repo mới, rồi:
cd ToeicLearning.Microservices
dotnet restore
dotnet build
```

### Bước 2 — Hạ tầng

```bash
docker compose up -d
```

### Bước 3 — Chạy tất cả service

```bash
chmod +x scripts/run-all.sh
./scripts/run-all.sh
```

Hoặc chạy từng project trong terminal riêng:

```bash
dotnet run --project src/Services/Identity/Identity.API
dotnet run --project src/Services/Catalog/Catalog.API
dotnet run --project src/Services/Exam/Exam.API
dotnet run --project src/Services/Progress/Progress.API
dotnet run --project src/ApiGateways/ApiGateway
dotnet run --project src/Web/Storefront.Web
```

### Bước 4 — Demo trên UI

1. Mở http://localhost:5100  
2. **Đăng ký** tài khoản (email + mật khẩu ≥ 6 ký tự)  
3. Vào **Khóa học** → xem seed data Catalog  
4. Vào **Luyện đề** → làm *Mini Part 5* → **Nộp bài**  
5. Vào **Tiến độ** (có thể F5 sau 1–2 giây) để thấy score từ Progress consumer  

Đáp án đúng mẫu Part 5: **B, C, A, D, B**

## Gateway routing

| Path trên Gateway | Downstream |
|---|---|
| `/identity/*` | Identity `:5001` |
| `/catalog/*` | Catalog `:5002` |
| `/exam/*` | Exam `:5003` |
| `/progress/*` | Progress `:5004` |

Ví dụ: `GET http://localhost:5080/catalog/api/courses`

## Gợi ý mở rộng đề tài

- Thêm Listening audio / upload file (Object storage)
- Payment / Subscription service
- Duende IdentityServer thay JWT đơn giản
- SQL Server / MongoDB thay SQLite từng service
- Kubernetes manifests + CI/CD
- gRPC giữa Exam ↔ Catalog
- Unit / Integration / Contract tests

## JWT demo

Tất cả service dùng chung key (chỉ cho demo local):

```text
ToeicLearning_SuperSecret_Key_For_Demo_2026!
Issuer: ToeicLearning.Identity
Audience: ToeicLearning.Clients
```
