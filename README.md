# TOEIC Learning Microservices

Project học TOEIC theo kiến trúc **microservices + Clean Architecture + Razor Pages**.

## Công nghệ demo

| Thành phần | Công nghệ |
|---|---|
| Frontend | ASP.NET Core **Razor Pages** |
| API Gateway | **YARP** Reverse Proxy |
| Auth | ASP.NET Identity + **JWT** |
| Catalog / Exam / Progress | **Clean Architecture** (Domain → Application → Infrastructure → API) |
| Database | **SQL Server 2022** + EF Core |
| Message bus | **RabbitMQ** (`ExamCompletedEvent`) |
| Cache | **Redis** (Progress summary) |
| Orchestration local | `docker compose` + `scripts/run-all.sh` |

## Cấu trúc solution

```text
ToeicLearning.Microservices/
├── ToeicLearning.sln
├── docker-compose.yml          # SQL Server + RabbitMQ + Redis và một số app
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
      → Web → YARP `/catalog/*` → Catalog.API (Clean Arch + SQL Server seed)

3. Luyện đề → Chi tiết đề → Start Full Test / Start Practice
      → Web → YARP `/exam/*` → Exam.API
      → lưu ExamAttempt / PracticeSession → trang làm bài
      → chọn đáp án, chuyển câu, bảng câu hỏi, đếm ngược tùy chế độ

4. Progress.API vẫn có consumer ExamCompletedEvent và API summary;
   luồng làm bài hiện tại chưa submit nên chưa phát event hoàn thành.
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
- Docker và Docker Compose (cho SQL Server, RabbitMQ, Redis)

### Chỉ xem homepage (không cần database hoặc Docker)

```bash
cd AcadPrep_v2_Microservice
dotnet run --project src/Web/Storefront.Web --no-launch-profile
```

Mở http://localhost:5100. Homepage giữ UI từ monolith, không gọi API.
Font, icon, ảnh và Tailwind hiện tải từ CDN nên cần Internet để hiển thị đầy đủ.
Các trang đăng nhập, khóa học, luyện đề và tiến độ cần chạy backend theo các bước dưới.
Các thẻ đề, điểm số và bảng xếp hạng trên homepage là nội dung minh họa từ bản cũ;
thẻ đề dẫn tới danh sách đề của service mới, bảng xếp hạng chưa có chức năng.

### Bước 1 — Clone / copy project

```bash
# copy cả thư mục ToeicLearning.Microservices sang repo mới, rồi:
cd AcadPrep_v2_Microservice
dotnet restore
dotnet build
```

### Bước 2 — Hạ tầng

```bash
docker compose up -d sqlserver rabbitmq redis
```

### Bước 3 — Chạy tất cả service

```bash
chmod +x scripts/run-all.sh
./scripts/run-all.sh
```

Script đợi hạ tầng sẵn sàng rồi chạy 6 ứng dụng local. Ctrl+C dừng các ứng dụng;
container hạ tầng vẫn chạy, dùng `docker compose stop sqlserver rabbitmq redis` để dừng.
Không chạy toàn bộ `docker compose up -d` cùng script vì sẽ trùng cổng 5003/5080/5100.

Hoặc chạy từng project trong terminal riêng (thêm `--no-launch-profile` để dùng đúng port appsettings):

```bash
dotnet run --project src/Services/Identity/Identity.API --no-launch-profile
dotnet run --project src/Services/Catalog/Catalog.API --no-launch-profile
dotnet run --project src/Services/Exam/Exam.API --no-launch-profile
dotnet run --project src/Services/Progress/Progress.API --no-launch-profile
dotnet run --project src/ApiGateways/ApiGateway --no-launch-profile
dotnet run --project src/Web/Storefront.Web --no-launch-profile
```

### Bước 4 — Demo trên UI

1. Mở http://localhost:5100  
2. Có thể làm bài bằng **phiên khách**, không cần chạy Identity.API hoặc đăng nhập
3. Vào **Khóa học** → xem seed data Catalog  
4. Vào **Luyện đề** → chọn đề → tab **Full Test** → **Start Full Test**.
5. Hoặc tab **Practice by Part** → chọn Part, topic (tùy chọn), thời gian (hoặc **No limit**) → **START PRACTICE**.
6. Chọn đáp án, dùng **Previous / Next** hoặc bảng số câu để di chuyển.

Full Test lấy toàn bộ câu hỏi và thời lượng từ đề (seed chuẩn: 200 câu, 120 phút;
một số đề seed rút gọn có ít câu hơn, UI hiển thị số câu thực tế).
Practice lọc theo Part và, nếu có chọn topic, chỉ lấy các câu khớp topic trong các Part đã chọn.
Giữ thứ tự nhóm câu hỏi và hiển thị ảnh, bài đọc chung (kể cả bộ nhiều bài đọc).
Khi hết giờ, UI khóa chọn đáp án. Mỗi lần Start tạo một phiên mới.

**Phạm vi hiện tại:** chưa play audio, submit/chấm điểm, lưu đáp án hoặc resume.
Đáp án chỉ nằm trong bộ nhớ trang, sẽ mất khi tải lại/rời trang. Không ghi đáp án đúng
hoặc giải thích trong response API làm bài.

Giao diện chi tiết đề, Full Test và Practice dùng lại Razor markup từ monolith.
Trang làm bài dùng riêng `_FullTestSession` / `_PracticeSession` với `_ExamTestAssets`
(font, màu, Tailwind, sidebar), không bọc bằng navbar/footer Storefront.
Bài đọc/ảnh nằm bên trái, đáp án bên phải; nhóm Part 3–4, Part 6 theo passage,
Part 7 theo question group hiển thị cùng bộ. Điều hướng dùng Prev/Next hoặc drawer.
Nút Submit giữ vị trí của bản gốc nhưng bị vô hiệu hóa; audio không hiển thị.
Full Test có thẻ giới thiệu, dùng bộ đếm giờ toàn đề và chuyển câu thủ công trong
phiên bản chưa có audio. Thoát trang có hộp thoại xác nhận mất đáp án.


Identity dùng UserId chuỗi. Exam đồng bộ UserId chuỗi cho `EXAM_ATTEMPTS` và
`PRACTICE_SESSIONS`; khi khởi động, seeder tự đổi cột legacy `int` sang `nvarchar(450)`
nếu cần, giữ dữ liệu cũ. ID mẫu `1/2/3` không đại diện cho tài khoản Identity thực.
Sau khi cập nhật source, khởi động lại Exam.API và Storefront.Web để dùng code mới.

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
- MongoDB cho các service phù hợp
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


## API bắt đầu phiên và tải câu hỏi

Mặc định bật `ExamSessions:AllowGuests` trong Exam.API. UI tự tạo mã khách ngẫu nhiên
và giữ trong Session của trình duyệt; không cần chạy Identity.API để làm bài.
Khi gọi API trực tiếp, gửi header `X-Exam-Guest-Key` với một UUID dạng 32 ký tự hex
(ví dụ tạo bằng `python3 -c 'import uuid; print(uuid.uuid4().hex)'`). Giữ nguyên mã
cho cả POST và GET của cùng phiên; mã này là bí mật truy cập phiên, không dùng chung.
Thiếu/sai định dạng mã trả 401; dùng mã khác đọc phiên trả 404.

Nếu đã có JWT hợp lệ, API ưu tiên UserId từ JWT. Muốn bắt buộc đăng nhập sau này,
đặt `ExamSessions:AllowGuests` thành `false`. API không nhận UserId từ body/query.
Mã khách được lưu với tiền tố `guest:` để tách khỏi tài khoản Identity.
Xóa cookie hoặc hết hạn Session sẽ mất quyền truy cập phiên khách từ trình duyệt đó.

| Method | Path trên Gateway | Chức năng |
|---|---|---|
| POST | `/exam/api/exams/{examId}/full-test` | Tạo Full Test, trả 201 `{ "sessionId": ... }` |
| GET | `/exam/api/full-tests/{sessionId}` | Tải câu hỏi Full Test của tài khoản hiện tại |
| POST | `/exam/api/exams/{examId}/practice` | Tạo Practice, trả 201 `{ "sessionId": ... }` |
| GET | `/exam/api/practice-sessions/{sessionId}` | Tải câu hỏi Practice của tài khoản hiện tại |

Body Practice:

```json
{
  "selectedPartNumbers": [5, 6],
  "selectedTags": [],
  "timeLimitMinutes": 30
}
```

Phải chọn ít nhất một Part trong 1–7. `selectedTags: []` bỏ lọc topic.
`timeLimitMinutes: null` không giới hạn; nếu đặt, nhận số nguyên 1–120 phút.
Đề chưa publish, không có câu hỏi, lựa chọn không hợp lệ hoặc không khớp câu hỏi trả 400.
GET trả `examTitle`, `startedAt`, `expiresAt` (UTC/null), `questions` kèm options,
ảnh và passages; không có đáp án đúng/giải thích/audio. GET phục vụ tải trang ban đầu,
chưa có khôi phục đáp án hay vị trí câu hỏi.

## Kiểm tra chức năng làm bài

```bash
dotnet test tests/Exam.Application.Tests
# Sau khi đã chạy Exam, Gateway và Web (không cần Identity):
python3 scripts/smoke-exam-sessions.py
# Hoặc chỉ định URL gateway và web riêng:
python3 scripts/smoke-exam-sessions.py http://localhost:5080 http://localhost:5100
```

Bộ test dùng SQLite in-memory để kiểm tra tạo phiên, quyền sở hữu, lựa chọn sai,
thời hạn UTC, bộ lọc Part/topic, bài đọc chung và không lộ đáp án.
Smoke test tạo phiên khách thử trong DB local; không gọi Identity.API.
Kiểm tra API qua gateway, quyền sở hữu mã khách, Razor Pages không đăng nhập,
antiforgery token và render trang câu hỏi của cả hai chế độ.
